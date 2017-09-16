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

        public static (CSharpCompilation, Assembly) Compile(string assemblyName, CSharpSyntaxTree syntaxTree, CompilationOptions options = null)
        {
            options = options ?? CompilationOptions.Default;

            var references = new MetadataReference[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location)
            };

            var optimizationLevel = options.DisableOptimization ? OptimizationLevel.Debug : OptimizationLevel.Release;

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

                var data = ms.ToArray();

                if (options.SaveAssembly)
                {
                    File.WriteAllBytes(assemblyName + ".dll", data);
                }

                var assembly = Assembly.Load(data);

                return (compilation, assembly);
            }
        }
    }

    internal class CompilationOptions
    {
        public static readonly CompilationOptions Default = new CompilationOptions();

        public bool SaveAssembly { get; set; }

        public bool DisableOptimization { get; set; }
    }
}