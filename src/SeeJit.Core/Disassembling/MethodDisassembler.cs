namespace SeeJit.Disassembling
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using Microsoft.Diagnostics.Runtime;
    using SharpDisasm;
    using SharpDisasm.Translators;
    using SharpDisassembler = SharpDisasm.Disassembler;

    internal class MethodDisassembler : Disassembler
    {
        public readonly MethodBase Method;

        private MethodDisassembler(MethodBase method)
        {
            Method = method;
        }

        public override void Disassemble(ClrRuntime runtime, TextWriter writer)
        {
            var method = runtime.GetMethodByAddress((ulong)Method.MethodHandle.GetFunctionPointer());
            if (method == null)
            {
                writer.Write("; Failed to load method '");

                var declaringType = Method.DeclaringType;
                if (declaringType != null)
                {
                    writer.Write(declaringType.FullName);
                    writer.Write('.');
                }

                writer.Write(Method.Name);
                writer.WriteLine("'.");

                return;
            }

            writer.Write("; ");
            writer.WriteLine(method.GetFullSignature());

            if (method.CompilationType == MethodCompilationType.None)
            {
                writer.WriteLine("; Failed to JIT compile this method.");
                writer.WriteLine("; Please see https://github.com/JeffreyZhao/SeeJit/issues/1 for more details.");

                return;
            }

            var methodAddress = method.HotColdInfo.HotStart;
            var methodSize = method.HotColdInfo.HotSize;
            var arch = runtime.ClrInfo.DacInfo.TargetArchitecture == Architecture.X86 ? ArchitectureMode.x86_32 : ArchitectureMode.x86_64;
            var instructions = Disassemble(methodAddress, methodSize, arch);
            var maxBytesLength = instructions.Max(i => i.Bytes.Length);
            var bytesColumnWidth = maxBytesLength * 2 + 2;
            var addressPrinter = arch == ArchitectureMode.x86_32 ? AddressPrinter.Short : AddressPrinter.Long;
            var translater = GetTranslater(runtime, addressPrinter);

            foreach (var ins in instructions)
            {
                PrintInstruction(writer, ins, addressPrinter, bytesColumnWidth, translater);
            }
        }

        private static List<Instruction> Disassemble(ulong methodAddress, uint methodSize, ArchitectureMode arch)
        {
            using (var disassembler = new SharpDisassembler((IntPtr)(long)methodAddress, (int)methodSize, arch, methodAddress, true))
            {
                return disassembler.Disassemble().ToList();
            }
        }

        private static Translator GetTranslater(ClrRuntime runtime, AddressPrinter addressPrinter)
        {
            return new IntelTranslator
            {
                SymbolResolver = (Instruction instruction, long address, ref long offset) =>
                {
                    var addr = (ulong)address;
                    var buffer = new StringWriter();

                    addressPrinter.Print(buffer, addr);

                    var operand = instruction.Operands.Length > 0 ? instruction.Operands[0] : null;
                    if (operand?.PtrOffset == 0)
                        return buffer.ToString();

                    var method = runtime.GetMethodByAddress(addr);
                    if (method != null)
                    {
                        buffer.Write(" (");
                        buffer.Write(method.GetFullSignature());
                        buffer.Write(")");
                    }

                    return buffer.ToString();
                }
            };
        }

        private static void PrintInstruction(TextWriter writer, Instruction instruction, AddressPrinter addressPrinter, int bytesColumnWidth, Translator translater)
        {
            // address
            addressPrinter.Print(writer, instruction.Offset);
            writer.Write("  ");

            // bytes
            foreach (var b in instruction.Bytes)
            {
                writer.Write(b.ToString("x2"));
            }

            for (var i = instruction.Bytes.Length * 2; i < bytesColumnWidth; i++)
            {
                writer.Write(' ');
            }

            // asm
            writer.WriteLine(translater.Translate(instruction));
        }

        public static MethodDisassembler Create(MethodBase method)
        {
            if (method.IsGenericMethod)
                return new OpenGeneric(method);

            RuntimeHelpers.PrepareMethod(method.MethodHandle);

            return new MethodDisassembler(method);
        }

        private class OpenGeneric : MethodDisassembler
        {
            public OpenGeneric(MethodBase method)
                : base(method) { }

            public override void Disassemble(ClrRuntime runtime, TextWriter writer)
            {
                writer.WriteLine($"; Open generic method '{Method}' cannot be JIT-compiled.");
            }
        }
    }
}