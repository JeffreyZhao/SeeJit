namespace SeeJit.Disassembling
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO;
    using System.Reflection;
    using Microsoft.Diagnostics.Runtime;
    using SeeJit.Collections;

    internal class TypeDisassembler : Disassembler
    {
        public readonly Type Type;
        public readonly ImmutableArray<Disassembler> Children;

        private TypeDisassembler(Type type)
        {
            Type = type;
        }

        private TypeDisassembler(Type type, IEnumerable<Disassembler> children)
            : this(type)
        {
            Children = ImmutableArray.CreateRange(children);
        }

        public override void Disassemble(ClrRuntime runtime, TextWriter writer)
        {
            if (Children.Length == 0)
                return;

            Children[0].Disassemble(runtime, writer);

            for (var i = 1; i < Children.Length; i++)
            {
                writer.WriteLine();

                Children[i].Disassemble(runtime, writer);
            }
        }

        public static TypeDisassembler Create(Type type, List<TreeItem<MemberInfo>> members)
        {
            if (type.IsGenericType)
                return new OpenGeneric(type);

            var children = new Disassembler[members.Count];

            for (var i = 0; i < members.Count; i++)
            {
                children[i] = Create(members[i]);
            }

            return new TypeDisassembler(type, children);
        }

        private class OpenGeneric : TypeDisassembler
        {
            public OpenGeneric(Type type)
                : base(type) { }

            public override void Disassemble(ClrRuntime runtime, TextWriter writer)
            {
                writer.WriteLine($"; Open generic type '{Type}' cannot be JIT-compiled.");
            }
        }
    }
}