namespace SeeJit.Disassembling
{
    using System.Diagnostics;
    using System.IO;

    internal abstract class AddressPrinter
    {
        public abstract void Print(TextWriter writer, ulong address);

        private class ShortPrinter : AddressPrinter
        {
            public override void Print(TextWriter writer, ulong address)
            {
                Debug.Assert(address <= uint.MaxValue);

                writer.Write(address.ToString("x8"));
            }
        }

        private class LongPrinter : AddressPrinter
        {
            private const ulong LowMask = uint.MaxValue;

            public override void Print(TextWriter writer, ulong address)
            {
                var high = address >> 32;
                var low = address & LowMask;

                writer.Write(high.ToString("x8"));
                writer.Write("`");
                writer.Write(low.ToString("x8"));
            }
        }

        public static readonly AddressPrinter Long = new LongPrinter();

        public static readonly AddressPrinter Short = new ShortPrinter();
    }
}