namespace SeeJit.Compilation
{
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.Text;

    internal class CSharpCompiler
    {
        public static CSharpSyntaxTree ParseText(string code)
        {
            return (CSharpSyntaxTree)CSharpSyntaxTree.ParseText(code);
        }

        public static CSharpSyntaxTree ParseFrom(string filepath)
        {
            using (var stream = File.OpenRead(filepath))
            {
                return (CSharpSyntaxTree)CSharpSyntaxTree.ParseText(SourceText.From(stream), path: filepath);
            }
        }

        public static CSharpCompilation Compile(string assemblyName, CSharpSyntaxTree syntaxTree, bool disableOptimization = false)
        {
            var references = new MetadataReference[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location)
            };

            var optimizationLevel = disableOptimization ? OptimizationLevel.Debug : OptimizationLevel.Release;

            var compilation = CSharpCompilation.Create(
                assemblyName,
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel : optimizationLevel));

            using (var ms = new MemoryStream())
            {
                var result = compilation.Emit(ms);
                if (!result.Success)
                    throw new CompilationException(result.Diagnostics);

                Assembly.Load(ms.ToArray());

                return compilation;
            }
        }
    }
}