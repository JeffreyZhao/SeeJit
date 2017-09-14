namespace SeeJit
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using Microsoft.Diagnostics.Runtime;
    using SeeJit.CodeAnalysis;
    using SeeJit.Compilation;
    using SeeJit.Disassembling;

    public static class SeeJitHelper
    {
        private static List<Disassembler> GetFileDisassemblers(DisassembleFileOptions options, TextWriter output)
        {
            var begin = DateTime.Now;
            var writer = options.IsVerbose ? MessageWriter.From(output) : MessageWriter.Empty;

            if (!options.IsVerbose)
            {
                output.Write("; Processing ... ");
            }

            // Parsing

            writer.Write("; Parsing from file ... ");

            var syntaxTree = CSharpCompiler.ParseFrom(options.FilePath);
            var parsed = DateTime.Now;

            writer.WriteLine($"done. ({Diff(begin, parsed)})");

            // Compiling

            writer.Write("; Compiling ... ");

            var assemblyName = $"{Path.GetFileName(options.FilePath)}-{Guid.NewGuid()}.dll";
            var compilation = CSharpCompiler.Compile(assemblyName, syntaxTree);
            var compiled = DateTime.Now;

            writer.WriteLine($"done. ({Diff(parsed, compiled)})");

            // Analyzing

            writer.Write("; Analyzing ... ");

            var syntaxItems = CSharpHelper.CollectSyntax(syntaxTree.GetRoot());
            var assembly = AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == assemblyName);
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var memberItems = CSharpHelper.CollectMembers(assembly, semanticModel, syntaxItems);
            var disassemblers = memberItems.Select(Disassembler.Create).ToList();
            var analyzed = DateTime.Now;

            writer.WriteLine($"done. ({Diff(compiled, analyzed)})");

            if (!options.IsVerbose)
            {
                output.WriteLine($"done. ({Diff(begin, analyzed)})");
            }

            return disassemblers;
        }

        public static void DisassembleFile(DisassembleFileOptions options, TextWriter output)
        {
            List<Disassembler> disassemblers;

            try
            {
                disassemblers = GetFileDisassemblers(options, output);
            }
            catch
            {
                output.WriteLine("failed!");

                throw;
            }

            using (var dt = DataTarget.AttachToProcess(Process.GetCurrentProcess().Id, UInt32.MaxValue, AttachFlag.Passive))
            {
                var runtime = dt.ClrVersions.Single().CreateRuntime();

                foreach (var disasm in disassemblers)
                {
                    output.WriteLine();

                    disasm.Disassemble(runtime, output);
                }
            }
        }

        private static string Diff(DateTime begin, DateTime end)
        {
            return (int)(end - begin).TotalMilliseconds + "ms";
        }

        private class MessageWriter
        {
            public static readonly MessageWriter Empty = new MessageWriter();

            protected MessageWriter() { }

            public virtual void Write(string message) { }

            public virtual void WriteLine(string message) { }

            private class TextMessageWriter : MessageWriter
            {
                private readonly TextWriter _writer;

                public TextMessageWriter(TextWriter writer)
                {
                    _writer = writer;
                }

                public override void Write(string message) => _writer.Write(message);

                public override void WriteLine(string message) => _writer.WriteLine(message);
            }

            public static MessageWriter From(TextWriter writer) => new TextMessageWriter(writer);
        }
    }

    public class DisassembleOptions
    {
        public bool IsVerbose { get; set; }
    }

    public class DisassembleFileOptions : DisassembleOptions
    {
        public string FilePath { get; set; }
    }
}