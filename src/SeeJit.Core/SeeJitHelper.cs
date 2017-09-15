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
        private static List<Disassembler> GetFileDisassemblers(DisassembleFileOptions options, MessageWriter writer)
        {
            var begin = DateTime.Now;

            // Parsing

            writer.Write("; Parsing from file ... ");

            var syntaxTree = CSharpCompiler.ParseFrom(options.FilePath);
            var parsed = DateTime.Now;

            writer.WriteLine($"done. ({Diff(begin, parsed)})");

            // Compiling

            writer.Write("; Compiling ... ");

            var assemblyName = Path.GetFileNameWithoutExtension(options.FilePath);
            var (_, assembly) = CSharpCompiler.Compile(assemblyName, syntaxTree, options.DisableOptimization);
            var compiled = DateTime.Now;

            writer.WriteLine($"done. ({Diff(parsed, compiled)})");

            // Analyzing

            writer.Write("; Analyzing ... ");

            var syntaxItems = CSharpHelper.CollectSyntax(syntaxTree.GetRoot());
            var memberItems = CSharpHelper.CollectMembers(assembly, syntaxItems);
            var disassemblers = memberItems.Select(Disassembler.Create).ToList();
            var analyzed = DateTime.Now;

            writer.WriteLine($"done. ({Diff(compiled, analyzed)})");
            writer.WriteLine("");

            return disassemblers;
        }

        public static void DisassembleFile(DisassembleFileOptions options, TextWriter output)
        {
            var writer = options.IsVerbose ? MessageWriter.From(output) : MessageWriter.Empty;

            List<Disassembler> disassemblers;

            try
            {
                disassemblers = GetFileDisassemblers(options, writer);
            }
            catch
            {
                writer.WriteLine("failed!");

                throw;
            }

            using (var dt = DataTarget.AttachToProcess(Process.GetCurrentProcess().Id, UInt32.MaxValue, AttachFlag.Passive))
            {
                var clr = dt.ClrVersions.Single();

                output.WriteLine(
                    "; {0:G} CLR {1} ({2}) on {3}.",
                    clr.Flavor, clr.Version, Path.GetFileName(clr.ModuleInfo.FileName), clr.DacInfo.TargetArchitecture.ToString("G").ToLowerInvariant()
                );

                var runtime = clr.CreateRuntime();

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

        public bool DisableOptimization { get; set; }
    }
}