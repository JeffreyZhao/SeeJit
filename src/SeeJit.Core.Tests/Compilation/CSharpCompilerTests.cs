namespace SeeJit.Tests.Compilation
{
    using SeeJit.Compilation;
    using Xunit;

    public class CSharpCompilerTests
    {
        [Fact]
        public void CompilePassed()
        {
            var code = "class TestClass { void TestMethod() { var i = 0; } }";

            Assert.NotNull(CSharpCompiler.Compile("test", CSharpCompiler.ParseText(code)));
        }

        [Fact]
        public void CompileFailed()
        {
            var code = "class TestClass { void TestMethod() { var i = 0 } }";

            var ex = Assert.Throws<CompilationException>(() => CSharpCompiler.Compile("test", CSharpCompiler.ParseText(code)));

            Assert.Contains("Error", ex.Message);
            Assert.Contains("; expected", ex.Message);
        }
    }
}