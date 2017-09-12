namespace SeeJit.Tests.Compilation
{
    using System.IO;
    using SeeJit.Compilation;
    using Xunit;

    public class CSharpCompilerTests
    {
        [Fact]
        public void CompilePassed()
        {
            var code = "class TestClass { void TestMethod() { var i = 0; } }";
            var errorWriter = new StringWriter();

            Assert.NotNull(CSharpCompiler.Compile("test.dll", code, errorWriter));
            Assert.Equal("", errorWriter.ToString());
        }

        [Fact]
        public void CompileFailed()
        {
            var code = "class TestClass { void TestMethod() { var i = 0 } }";
            var errorWriter = new StringWriter();

            Assert.Null(CSharpCompiler.Compile("test.dll", code, errorWriter));

            var errorMessage = errorWriter.ToString();

            Assert.Contains("Error", errorMessage);
            Assert.Contains("; expected", errorMessage);
        }
    }
}