namespace SeeJit.Tests.CodeAnalysis
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using SeeJit.CodeAnalysis;
    using SeeJit.Collections;
    using SeeJit.Compilation;
    using Xunit;

    public class CSharpHelperTests
    {
        public class CollectSyntax
        {
            private static List<TreeItem<CSharpSyntaxNode>> Collect(string code)
            {
                var syntaxTree = CSharpSyntaxTree.ParseText(code);
                return CSharpHelper.CollectSyntax(syntaxTree.GetRoot());
            }

            [Fact]
            public void ClassesAndStructsButNoInterfaces()
            {
                var code = @"
class MyClass
{
    public void Method1() { }
}

struct MyStruct
{
    public void Method2() { }
}

interface IMyInterface
{
    void Method3() { }
}";

                var treeItems = Collect(code);

                Assert.Equal(2, treeItems.Count);

                var myClassItem = treeItems[0];
                var myClassDecl = Assert.IsType<ClassDeclarationSyntax>(myClassItem.Value);
                Assert.Equal("MyClass", myClassDecl.Identifier.Text);

                Assert.Equal(1, myClassItem.Children.Count);
                var myClassMethod1Decl = Assert.IsType<MethodDeclarationSyntax>(myClassItem.Children[0].Value);
                Assert.Equal("Method1", myClassMethod1Decl.Identifier.Text);

                var myStructItem = treeItems[1];
                var myStructDecl = Assert.IsType<StructDeclarationSyntax>(myStructItem.Value);
                Assert.Equal("MyStruct", myStructDecl.Identifier.Text);

                Assert.Equal(1, myStructItem.Children.Count);
                var myStructMethod2Decl = Assert.IsType<MethodDeclarationSyntax>(myStructItem.Children[0].Value);
                Assert.Equal("Method2", myStructMethod2Decl.Identifier.Text);
            }

            [Fact]
            public void WithNamespaces()
            {
                var code = @"
namespace Namespace1
{
    class MyClass { }

    namespace Namespace2
    {
        struct MyStruct { }
    }
}

class GlobalClass { }";

                var treeItems = Collect(code);

                Assert.Equal(3, treeItems.Count);
                Assert.Equal("MyClass", Assert.IsType<ClassDeclarationSyntax>(treeItems[0].Value).Identifier.Text);
                Assert.Equal("MyStruct", Assert.IsType<StructDeclarationSyntax>(treeItems[1].Value).Identifier.Text);
                Assert.Equal("GlobalClass", Assert.IsType<ClassDeclarationSyntax>(treeItems[2].Value).Identifier.Text);
            }

            [Fact]
            public void NestedTypes()
            {
                var code = @"
class MyClass
{
    void MyMethod1();

    struct MyStruct
    {
        void MyMethod3();
    }

    void MyMethod2();
}";

                var treeItems = Collect(code);

                Assert.Equal(1, treeItems.Count);
                var myClassItem = treeItems[0];
                Assert.Equal("MyClass", Assert.IsType<ClassDeclarationSyntax>(myClassItem.Value).Identifier.Text);

                Assert.Equal(3, myClassItem.Children.Count);
                var myStructItem = myClassItem.Children[1];
                Assert.Equal("MyMethod1", Assert.IsType<MethodDeclarationSyntax>(myClassItem.Children[0].Value).Identifier.Text);
                Assert.Equal("MyStruct", Assert.IsType<StructDeclarationSyntax>(myStructItem.Value).Identifier.Text);
                Assert.Equal("MyMethod2", Assert.IsType<MethodDeclarationSyntax>(myClassItem.Children[2].Value).Identifier.Text);

                Assert.Equal(1, myStructItem.Children.Count);
                Assert.Equal("MyMethod3", Assert.IsType<MethodDeclarationSyntax>(myStructItem.Children[0].Value).Identifier.Text);
            }

            [Fact]
            public void Constructors()
            {
                var code = @"
class MyClass {
    private MyClass() { }
    static MyClass() { }
    public MyClass(string s) { }
    protected MyClass(int i, long j) { }
}";
                var treeItems = Collect(code);

                Assert.Equal(1, treeItems.Count);

                var ctorItems = treeItems[0].Children;

                Assert.Equal(4, ctorItems.Count);

                var ctor0 = Assert.IsType<ConstructorDeclarationSyntax>(ctorItems[0].Value);
                Assert.Equal("private", ctor0.Modifiers.Single().Text);
                Assert.Equal(0, ctor0.ParameterList.Parameters.Count);

                var ctor1 = Assert.IsType<ConstructorDeclarationSyntax>(ctorItems[1].Value);
                Assert.Equal("static", ctor1.Modifiers.Single().Text);
                Assert.Equal(0, ctor1.ParameterList.Parameters.Count);

                var ctor2 = Assert.IsType<ConstructorDeclarationSyntax>(ctorItems[2].Value);
                Assert.Equal("public", ctor2.Modifiers.Single().Text);
                Assert.Equal(1, ctor2.ParameterList.Parameters.Count);
                Assert.Equal("string", ctor2.ParameterList.Parameters[0].Type.ToString());
                Assert.Equal("s", ctor2.ParameterList.Parameters[0].Identifier.Text);

                var ctor3 = Assert.IsType<ConstructorDeclarationSyntax>(ctorItems[3].Value);
                Assert.Equal("protected", ctor3.Modifiers.Single().Text);
                Assert.Equal(2, ctor3.ParameterList.Parameters.Count);
                Assert.Equal("int", ctor3.ParameterList.Parameters[0].Type.ToString());
                Assert.Equal("i", ctor3.ParameterList.Parameters[0].Identifier.Text);
                Assert.Equal("long", ctor3.ParameterList.Parameters[1].Type.ToString());
                Assert.Equal("j", ctor3.ParameterList.Parameters[1].Identifier.Text);
            }

            [Fact]
            public void InterfaceMethods()
            {
                var code = @"
interface IMyClass
{
    void MyMethod();
}

class MyClass : IMyClass
{
    void MyMethod() { }

    void IMyClass.MyMethod() { }
}";
                var items = Collect(code);

                Assert.Equal(1, items.Count);

                var methodItems = items[0].Children;

                Assert.Equal(2, methodItems.Count);

                var method0 = Assert.IsType<MethodDeclarationSyntax>(methodItems[0].Value);
                var method1 = Assert.IsType<MethodDeclarationSyntax>(methodItems[1].Value);

                Assert.Equal("MyMethod", method0.Identifier.Text);
                Assert.Null(method0.ExplicitInterfaceSpecifier);

                Assert.Equal("MyMethod", method0.Identifier.Text);
                Assert.NotNull(method1.ExplicitInterfaceSpecifier);
            }

            [Fact]
            public void Properites()
            {
                var code = @"
class MyClass {
    int Property1
    {
        get { return 0; }
        set { }
    }

    long Property2
    {
        get { return 0L; }
    }

    long PropertyIgnore { get; set; }

    string Property3
    {
        set { }
        get { return null; }
    }
}";
                var treeItems = Collect(code);

                Assert.Equal(1, treeItems.Count);

                var accessorItems = treeItems[0].Children;

                Assert.Equal(5, accessorItems.Count);

                var property1Getter = Assert.IsType<AccessorDeclarationSyntax>(accessorItems[0].Value);
                Assert.Equal("get", property1Getter.Keyword.Text);
                Assert.Equal("Property1", Assert.IsType<PropertyDeclarationSyntax>(property1Getter.Parent.Parent).Identifier.Text);

                var property1Setter = Assert.IsType<AccessorDeclarationSyntax>(accessorItems[1].Value);
                Assert.Equal("set", property1Setter.Keyword.Text);
                Assert.Equal("Property1", Assert.IsType<PropertyDeclarationSyntax>(property1Setter.Parent.Parent).Identifier.Text);

                var property2Getter = Assert.IsType<AccessorDeclarationSyntax>(accessorItems[2].Value);
                Assert.Equal("get", property2Getter.Keyword.Text);
                Assert.Equal("Property2", Assert.IsType<PropertyDeclarationSyntax>(property2Getter.Parent.Parent).Identifier.Text);

                var property3Setter = Assert.IsType<AccessorDeclarationSyntax>(accessorItems[3].Value);
                Assert.Equal("set", property3Setter.Keyword.Text);
                Assert.Equal("Property3", Assert.IsType<PropertyDeclarationSyntax>(property3Setter.Parent.Parent).Identifier.Text);

                var property3Getter = Assert.IsType<AccessorDeclarationSyntax>(accessorItems[4].Value);
                Assert.Equal("get", property1Getter.Keyword.Text);
                Assert.Equal("Property3", Assert.IsType<PropertyDeclarationSyntax>(property3Getter.Parent.Parent).Identifier.Text);
            }

            [Fact]
            public void Indexers()
            {
                var code = @"
class MyClass
{
    int this[int i]
    {
        get { return i; }
        set { }
    }

    string this[int i, int j]
    {
        get { return (i + j).ToString(); }
    }
}";
                var memberItems = Collect(code);

                Assert.Equal(1, memberItems.Count);

                var accessorItems = memberItems[0].Children;

                Assert.Equal(3, accessorItems.Count);

                var getter0 = Assert.IsAssignableFrom<AccessorDeclarationSyntax>(accessorItems[0].Value);
                var setter0 = Assert.IsAssignableFrom<AccessorDeclarationSyntax>(accessorItems[1].Value);
                var getter1 = Assert.IsAssignableFrom<AccessorDeclarationSyntax>(accessorItems[2].Value);

                Assert.Same(getter0.Parent, setter0.Parent);
                Assert.Equal(1, Assert.IsAssignableFrom<IndexerDeclarationSyntax>(getter0.Parent.Parent).ParameterList.Parameters.Count);

                Assert.Equal(2, Assert.IsAssignableFrom<IndexerDeclarationSyntax>(getter1.Parent.Parent).ParameterList.Parameters.Count);
            }
        }

        public class CollectMembers
        {
            private static List<TreeItem<MemberInfo>> Collect(string code)
            {
                var assemblyName = Guid.NewGuid().ToString();
                var syntaxTree = CSharpCompiler.ParseText(code);
                var compilation = CSharpCompiler.Compile(assemblyName, syntaxTree);
                var assembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == assemblyName);
                var syntaxItems = CSharpHelper.CollectSyntax(syntaxTree.GetRoot());

                return CSharpHelper.CollectMembers(assembly, compilation.GetSemanticModel(syntaxTree), syntaxItems);
            }

            [Fact]
            public void ClassesAndStructsButNoInterfaces()
            {
                var code = @"
class MyClass { }
struct MyStruct { }
interface MyInterface { }";

                var members = Collect(code);

                Assert.Equal(2, members.Count);
                Assert.Equal("MyClass", members[0].Value.Name);
                Assert.Equal("MyStruct", members[1].Value.Name);
            }

            [Fact]
            public void WithNamespaces()
            {
                var code = @"
namespace Ns1.Ns2 {
    class MyClass { }

    namespace Ns3
    {
        struct MyStruct { }
    }
}

class GlobalClass { }";

                var members = Collect(code);

                Assert.Equal(3, members.Count);
                Assert.Equal("MyClass", members[0].Value.Name);
                Assert.Equal("MyStruct", members[1].Value.Name);
                Assert.Equal("GlobalClass", members[2].Value.Name);
            }

            [Fact]
            public void GenericTypes()
            {
                var code = @"
namespace Ns1.Ns2
{
    class MyClass { }
    class MyClass<T> { }
    class MyClass<T1, T2> { }
}";
                var members = Collect(code);

                Assert.Equal(3, members.Count);
                Assert.Equal(0, Assert.IsAssignableFrom<Type>(members[0].Value).GetGenericArguments().Length);
                Assert.Equal(1, Assert.IsAssignableFrom<Type>(members[1].Value).GetGenericArguments().Length);
                Assert.Equal(2, Assert.IsAssignableFrom<Type>(members[2].Value).GetGenericArguments().Length);
            }
            
            [Fact]
            public void NestedTypes()
            {
                var code = @"
class Parent
{
    class Child { }
}";
                var members = Collect(code);

                Assert.Equal(1, members.Count);
                Assert.Equal("Parent", members[0].Value.Name);
                Assert.Equal(1, members[0].Children.Count);
                Assert.Equal("Child", members[0].Children[0].Value.Name);
            }

            [Fact]
            public void NestedGenericTypes()
            {
                var code = @"
class Parent
{
    class Child<T1, T2> { }
}

class Parent<T1>
{
    class Child<T2> { }
}";
                var members = Collect(code);

                Assert.Equal(2, members.Count);

                Assert.Equal("Parent", members[0].Value.Name);
                Assert.Equal(1, members[0].Children.Count);
                Assert.Equal("Child`2", members[0].Children[0].Value.Name);

                Assert.Equal("Parent`1", members[1].Value.Name);
                Assert.Equal(1, members[1].Children.Count);
                Assert.Equal("Child`1", members[1].Children[0].Value.Name);
            }

            [Fact]
            public void Constructors()
            {
                var code = @"
namespace Ns1.Ns2
{
    class MyClass
    {
        MyClass(int i, long l) {}
        static MyClass() { }
        MyClass(string s, double d) { }
    }
}";
                var members = Collect(code);

                Assert.Equal(1, members.Count);
                Assert.Equal("MyClass", members[0].Value.Name);

                var ctorItems = members[0].Children;

                Assert.Equal(3, ctorItems.Count);

                var ctor0 = Assert.IsAssignableFrom<ConstructorInfo>(ctorItems[0].Value);
                var ctor1 = Assert.IsAssignableFrom<ConstructorInfo>(ctorItems[1].Value);
                var ctor2 = Assert.IsAssignableFrom<ConstructorInfo>(ctorItems[2].Value);

                var parameters0 = ctor0.GetParameters();
                Assert.Equal(2, parameters0.Length);
                Assert.Equal(typeof(int), parameters0[0].ParameterType);
                Assert.Equal(typeof(long), parameters0[1].ParameterType);

                Assert.True(ctor1.IsStatic);

                var parameters2 = ctor2.GetParameters();
                Assert.Equal(2, parameters2.Length);
                Assert.Equal(typeof(string), parameters2[0].ParameterType);
                Assert.Equal(typeof(double), parameters2[1].ParameterType);
            }

            [Fact]
            public void Methods()
            {
                var code = @"
namespace Ns1.Ns2
{
    class MyClass
    {
        static int MyMethod() { return 0; }
        void MyMethod(int i) { }
        long MyMethod(string s) { return 0L; }

        void NoOverload() { }
    }
}";
                var members = Collect(code);

                Assert.Equal(1, members.Count);
                Assert.Equal("MyClass", members[0].Value.Name);

                var methodItems = members[0].Children;

                Assert.Equal(4, methodItems.Count);

                Assert.Equal(typeof(int), Assert.IsAssignableFrom<MethodInfo>(methodItems[0].Value).ReturnType);
                Assert.Equal(typeof(void), Assert.IsAssignableFrom<MethodInfo>(methodItems[1].Value).ReturnType);
                Assert.Equal(typeof(long), Assert.IsAssignableFrom<MethodInfo>(methodItems[2].Value).ReturnType);
                Assert.Equal("NoOverload", Assert.IsAssignableFrom<MethodInfo>(methodItems[3].Value).Name);
            }

            [Fact]
            public void GenericMethods()
            {
                var code = @"
namespace Ns1.Ns2
{
    class MyClass
    {
        void MyMethod<T>(T t) { }
        T2 MyMethod<T1, T2>(T1 t1) { return default(T2); }
        void MyMethod<T>(T a, T b) { }
    }
}";
                var members = Collect(code);

                Assert.Equal(1, members.Count);
                Assert.Equal("MyClass", members[0].Value.Name);

                var methodItems = members[0].Children;

                Assert.Equal(3, methodItems.Count);

                var method0 = Assert.IsAssignableFrom<MethodInfo>(methodItems[0].Value);
                var method1 = Assert.IsAssignableFrom<MethodInfo>(methodItems[1].Value);
                var method2 = Assert.IsAssignableFrom<MethodInfo>(methodItems[2].Value);

                Assert.Equal(1, method0.GetGenericArguments().Length);
                Assert.Equal(1, method0.GetParameters().Length);
                Assert.Equal(typeof(void), method0.ReturnType);

                Assert.Equal(2, method1.GetGenericArguments().Length);
                Assert.Equal(1, method1.GetParameters().Length);
                Assert.Equal("T2", method1.ReturnType.Name);

                Assert.Equal(1, method2.GetGenericArguments().Length);
                Assert.Equal(2, method2.GetParameters().Length);
                Assert.Equal(typeof(void), method2.ReturnType);
            }

            [Fact]
            public void InterfaceMethods()
            {
                var code = @"
namespace MyNamespace
{
    interface IMyClass<T>
    {
        void MyMethod();
    }

    class MyClass : IMyClass<int>, IMyClass<string>
    {
        void IMyClass<string>.MyMethod() { }

        void MyMethod() { }

        void IMyClass<int>.MyMethod() { }
    }
}";
                var items = Collect(code);

                Assert.Equal(1, items.Count);

                var methodItems = items[0].Children;

                Assert.Equal(3, methodItems.Count);

                var method0 = Assert.IsAssignableFrom<MethodInfo>(methodItems[0].Value);
                var method1 = Assert.IsAssignableFrom<MethodInfo>(methodItems[1].Value);
                var method2 = Assert.IsAssignableFrom<MethodInfo>(methodItems[2].Value);

                Assert.Equal("MyNamespace.IMyClass<System.String>.MyMethod", method0.Name);
                Assert.Equal("MyMethod", method1.Name);
                Assert.Equal("MyNamespace.IMyClass<System.Int32>.MyMethod", method2.Name);
            }

            [Fact]
            public void Properties()
            {
                var code = @"
namespace Ns1.Ns2
{
    class MyClass
    {
        static int MyProperty1
        {
            get { return 0; }
            set { }
        }

        string MyProperty2
        {
            get { return null; }
        }
    }
}";
                var members = Collect(code);

                Assert.Equal(1, members.Count);
                Assert.Equal("MyClass", members[0].Value.Name);

                var propertyItems = members[0].Children;

                Assert.Equal("get_MyProperty1", Assert.IsAssignableFrom<MethodInfo>(propertyItems[0].Value).Name);
                Assert.Equal("set_MyProperty1", Assert.IsAssignableFrom<MethodInfo>(propertyItems[1].Value).Name);
                Assert.Equal("get_MyProperty2", Assert.IsAssignableFrom<MethodInfo>(propertyItems[2].Value).Name);
            }

            [Fact]
            public void Indexers()
            {
                var code = @"
class MyClass
{
    double this[int i]
    {
        get { return i; }
        set { }
    }

    string this[int i, decimal j]
    {
        get { return (i + j).ToString(); }
    }
}";
                var memberItems = Collect(code);

                Assert.Equal(1, memberItems.Count);

                var accessorItems = memberItems[0].Children;

                Assert.Equal(3, accessorItems.Count);

                var accessor0 = Assert.IsAssignableFrom<MethodInfo>(accessorItems[0].Value);
                var accessor1 = Assert.IsAssignableFrom<MethodInfo>(accessorItems[1].Value);
                var accessor2 = Assert.IsAssignableFrom<MethodInfo>(accessorItems[2].Value);

                Assert.Equal(typeof(double), accessor0.ReturnType);
                Assert.Equal(new[] { typeof(int) }, accessor0.GetParameters().Select(p => p.ParameterType));

                Assert.Equal(typeof(void), accessor1.ReturnType);
                Assert.Equal(new[] { typeof(int), typeof(double) }, accessor1.GetParameters().Select(p => p.ParameterType));

                Assert.Equal(typeof(string), accessor2.ReturnType);
                Assert.Equal(new[]{typeof(int), typeof(decimal)}, accessor2.GetParameters().Select(p => p.ParameterType));
            }
        }
    }
}