namespace SeeJit.CodeAnalysis
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using SeeJit.Collections;
    using SeeJit.Reflection;

    internal static class CSharpHelper
    {
        public static List<TreeItem<CSharpSyntaxNode>> CollectSyntax(SyntaxNode root)
        {
            return new CSharpSyntaxNodeCollector().Collect(root);
        }

        private class CSharpSyntaxNodeCollector : CSharpSyntaxWalker
        {
            private readonly Stack<List<TreeItem<CSharpSyntaxNode>>> _stack = new Stack<List<TreeItem<CSharpSyntaxNode>>>();

            public List<TreeItem<CSharpSyntaxNode>> Collect(SyntaxNode root)
            {
                Debug.Assert(_stack.Count == 0);

                _stack.Push(new List<TreeItem<CSharpSyntaxNode>>());

                Visit(root);

                Debug.Assert(_stack.Count == 1);

                return _stack.Pop();
            }

            public override void VisitClassDeclaration(ClassDeclarationSyntax node)
            {
                var treeItem = new TreeItem<CSharpSyntaxNode>(node, true);
                
                _stack.Peek().Add(treeItem);
                _stack.Push(treeItem.Children);

                base.VisitClassDeclaration(node);

                _stack.Pop();
            }

            public override void VisitStructDeclaration(StructDeclarationSyntax node)
            {
                var treeItem = new TreeItem<CSharpSyntaxNode>(node, true);

                _stack.Peek().Add(treeItem);
                _stack.Push(treeItem.Children);

                base.VisitStructDeclaration(node);

                _stack.Pop();
            }

            public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
            {
                if (node.Parent is InterfaceDeclarationSyntax)
                    return;

                _stack.Peek().Add(new TreeItem<CSharpSyntaxNode>(node));
            }

            public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
            {
                _stack.Peek().Add(new TreeItem<CSharpSyntaxNode>(node));
            }

            public override void VisitAccessorDeclaration(AccessorDeclarationSyntax node)
            {
                if (node.Body != null)
                {
                    _stack.Peek().Add(new TreeItem<CSharpSyntaxNode>(node));
                }
            }
        }

        private static readonly BindingFlags BindingFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        public static List<TreeItem<MemberInfo>> CollectMembers(Assembly assembly, SemanticModel model, List<TreeItem<CSharpSyntaxNode>> syntaxItems)
        {
            var list = new List<TreeItem<MemberInfo>>(syntaxItems.Count);

            foreach (var item in syntaxItems)
            {
                var type = GetType(assembly, (TypeDeclarationSyntax)item.Value);
                var children = CollectMembers(type, model, item.Children);

                list.Add(new TreeItem<MemberInfo>(type, children));
            }

            return list;
        }

        private static List<TreeItem<MemberInfo>> CollectMembers(Type parentType, SemanticModel model, List<TreeItem<CSharpSyntaxNode>> syntaxItems)
        {
            var list = new List<TreeItem<MemberInfo>>(syntaxItems.Count);

            Dictionary<string, object> allMethods = null;

            foreach (var item in syntaxItems)
            {
                list.Add(GetMemberItem(parentType, model, item, ref allMethods));
            }

            return list;
        }

        private static TreeItem<MemberInfo> GetMemberItem(Type parentType, SemanticModel model, TreeItem<CSharpSyntaxNode> syntaxItem, ref Dictionary<string, object> allMethods)
        {
            var syntax = syntaxItem.Value;

            var typeDecl = syntax as TypeDeclarationSyntax;
            if (typeDecl != null)
            {
                var type = parentType.GetNestedType(GetTypeName(typeDecl), BindingFlags);
                var children = CollectMembers(type, model, syntaxItem.Children);

                return new TreeItem<MemberInfo>(type, children);
            }

            var ctorDecl = syntax as ConstructorDeclarationSyntax;
            if (ctorDecl != null)
            {
                var isStatic = ctorDecl.Modifiers.Any(m => m.Kind() == SyntaxKind.StaticKeyword);
                var ctor = isStatic
                    ? (MemberInfo)GetAllMethods(parentType, ref allMethods)[".cctor"]
                    : FindMethod(GetAllMethods(parentType, ref allMethods), model, ctorDecl);

                return new TreeItem<MemberInfo>(ctor);
            }

            var methodDecl = syntax as MethodDeclarationSyntax;
            if (methodDecl != null)
            {
                var method = FindMethod(GetAllMethods(parentType, ref allMethods), model, methodDecl);
                return new TreeItem<MemberInfo>(method);
            }

            var accessorName = GetAccessorName((AccessorDeclarationSyntax)syntax);
            return new TreeItem<MemberInfo>((MethodBase)GetAllMethods(parentType, ref allMethods)[accessorName]);
        }

        private static MethodBase FindMethod(Dictionary<string, object> allMethods, SemanticModel model, BaseMethodDeclarationSyntax syntax)
        {
            var methodDecl = syntax as MethodDeclarationSyntax;
            var methodName = methodDecl == null ? ".ctor" : GetMethodName(methodDecl);
            var methodOrList = allMethods[methodName];

            var overloads = methodOrList as List<MethodBase>;
            if (overloads == null)
                return (MethodBase)methodOrList;

            Debug.Assert(overloads.Count > 1);

            var symbol = model.GetDeclaredSymbol(syntax);
            return overloads.Find(m => symbol.IsSame(m));
        }

        private static Dictionary<string, object> GetAllMethods(Type type, ref Dictionary<string, object> allMethods)
        {
            if (allMethods == null)
            {
                allMethods = new Dictionary<string, object>();

                foreach (var method in type.GetMembers(BindingFlags).OfType<MethodBase>().Where(m => !m.IsCompilerGenerated()))
                {
                    var methodName = method.Name;
                    if (method.IsGenericMethod)
                    {
                        methodName = methodName + "`" + method.GetGenericArguments().Length;
                    }

                    if (!allMethods.TryGetValue(methodName, out object methodOrList))
                    {
                        allMethods.Add(methodName, method);
                        continue;
                    }

                    var list = methodOrList as List<MethodBase>;
                    if (list != null)
                    {
                        list.Add(method);
                        continue;
                    }

                    allMethods[methodName] = new List<MethodBase> { (MethodBase)methodOrList, method };
                }
            }

            return allMethods;
        }

        private static Type GetType(Assembly assembly, TypeDeclarationSyntax node)
        {
            var sb = new StringBuilder(100);

            sb.AppendNamespace(node.Parent as NamespaceDeclarationSyntax).Append(GetTypeName(node));

            return assembly.GetType(sb.ToString());
        }

        private static StringBuilder AppendNamespace(this StringBuilder sb, NamespaceDeclarationSyntax nsSyntax)
        {
            if (nsSyntax == null)
                return sb;

            return sb.AppendNamespace(nsSyntax.Parent as NamespaceDeclarationSyntax).Append(nsSyntax.Name).Append(".");
        }

        private static string GetTypeName(TypeDeclarationSyntax node)
        {
            if (node.TypeParameterList == null)
                return node.Identifier.Text;

            return node.Identifier.Text + "`" + node.TypeParameterList.Parameters.Count;
        }

        private static string GetMethodName(MethodDeclarationSyntax node)
        {
            if (node.TypeParameterList == null)
                return node.Identifier.Text;

            return node.Identifier.Text + "`" + node.TypeParameterList.Parameters.Count;
        }

        private static string GetAccessorName(AccessorDeclarationSyntax node)
        {
            var propertyName = ((PropertyDeclarationSyntax)node.Parent.Parent).Identifier;
            var prefix = node.Kind() == SyntaxKind.GetAccessorDeclaration ? "get_" : "set_";
            return prefix + propertyName;
        }
    }
}
