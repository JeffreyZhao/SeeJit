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

        public static List<TreeItem<MemberInfo>> CollectMembers(Assembly assembly, List<TreeItem<CSharpSyntaxNode>> syntaxItems)
        {
            var list = new List<TreeItem<MemberInfo>>(syntaxItems.Count);

            foreach (var item in syntaxItems)
            {
                var type = GetType(assembly, (TypeDeclarationSyntax)item.Value);
                var children = CollectMembers(type, item.Children);

                list.Add(new TreeItem<MemberInfo>(type, children));
            }

            return list;
        }

        private const BindingFlags MemberBindingFlags = 
            BindingFlags.Instance |
            BindingFlags.Static |
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.DeclaredOnly;

        private static List<TreeItem<MemberInfo>> CollectMembers(Type parentType, List<TreeItem<CSharpSyntaxNode>> syntaxItems)
        {
            var list = new List<TreeItem<MemberInfo>>(syntaxItems.Count);
            var methods = (MethodsOfType)null;

            foreach (var item in syntaxItems)
            {
                list.Add(GetMemberItem(parentType, item, ref methods));
            }

            return list;
        }

        private static TreeItem<MemberInfo> GetMemberItem(Type parentType, TreeItem<CSharpSyntaxNode> syntaxItem, ref MethodsOfType methods)
        {
            var syntax = syntaxItem.Value;

            var typeDecl = syntax as TypeDeclarationSyntax;
            if (typeDecl != null)
            {
                var type = parentType.GetNestedType(GetTypeName(typeDecl), MemberBindingFlags);
                var children = CollectMembers(type, syntaxItem.Children);

                return new TreeItem<MemberInfo>(type, children);
            }

            var method = FindMethod(syntax, parentType, ref methods);

            return new TreeItem<MemberInfo>(method);
        }

        private static MethodBase FindMethod(CSharpSyntaxNode node, Type type, ref MethodsOfType methods)
        {
            if (methods == null)
            {
                methods = new MethodsOfType(type);
            }
            
            // Here's a trick to keep the order of overloaded methods. The trick relies on an
            // implementation detail of Type.GetMembers() method, that it returns the methods
            // with the same order in declaration. So if we group by methods by their names,
            // and find them with the declaration order, we'll get the correct result without
            // comparing symbols to the methods.

            return methods[GetMethodName(node)].Dequeue();
        }

        private class MethodsOfType : Dictionary<string, Queue<MethodBase>>
        {
            public MethodsOfType(Type type)
            {
                var methodsByName = type
                    .GetMembers(MemberBindingFlags)
                    .OfType<MethodBase>()
                    .Where(m => !m.IsCompilerGenerated())
                    .GroupBy(GetMethodName);

                foreach (var group in methodsByName)
                {
                    Add(group.Key, new Queue<MethodBase>(group));
                }
            }
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

        private static string GetMethodName(MethodBase method)
        {
            var methodName = method.Name;

            var dotIndex = methodName.LastIndexOf('.');
            if (dotIndex > 0) // explicit implementation of an interface method
            {
                methodName = methodName.Substring(dotIndex + 1);
            }

            if (method.IsGenericMethod)
            {
                methodName = methodName + "`" + method.GetGenericArguments().Length;
            }

            return methodName;
        }

        private static string GetMethodName(CSharpSyntaxNode node)
        {
            var methodDecl = node as MethodDeclarationSyntax;
            if (methodDecl != null)
                return GetMethodName(methodDecl);

            var accessorDecl = node as AccessorDeclarationSyntax;
            if (accessorDecl != null)
                return GetAccessorName(accessorDecl);

            var ctorDecl = node as ConstructorDeclarationSyntax;
            if (ctorDecl != null)
                return GetConsturctorName(ctorDecl);

            throw new NotSupportedException("Unsupported node type: " + node.GetType().Name);
        }

        private static string GetMethodName(MethodDeclarationSyntax node)
        {
            if (node.TypeParameterList == null)
                return node.Identifier.Text;

            return node.Identifier.Text + "`" + node.TypeParameterList.Parameters.Count;
        }

        private static string GetAccessorName(AccessorDeclarationSyntax node)
        {
            var prefix = node.Kind() == SyntaxKind.GetAccessorDeclaration ? "get_" : "set_";

            var indexerDecl = node.Parent.Parent as IndexerDeclarationSyntax;
            if (indexerDecl != null)
                return prefix + "Item";

            var propertyName = ((PropertyDeclarationSyntax)node.Parent.Parent).Identifier;
            return prefix + propertyName;
        }

        private static string GetConsturctorName(ConstructorDeclarationSyntax node)
        {
            var isStatic = node.Modifiers.Any(m => m.Kind() == SyntaxKind.StaticKeyword);
            return isStatic ? ".cctor" : ".ctor";
        }
    }
}
