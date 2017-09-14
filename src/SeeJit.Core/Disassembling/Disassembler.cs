namespace SeeJit.Disassembling
{
    using System;
    using System.IO;
    using System.Reflection;
    using Microsoft.Diagnostics.Runtime;
    using SeeJit.Collections;

    internal abstract class Disassembler
    {
        public abstract void Disassemble(ClrRuntime runtime, TextWriter writer);

        public static Disassembler Create(TreeItem<MemberInfo> memberItem)
        {
            var member = memberItem.Value;
            var method = member as MethodBase;
            return method != null
                ? (Disassembler)MethodDisassembler.Create(method)
                : TypeDisassembler.Create((Type)member, memberItem.Children);
        }
    }
}