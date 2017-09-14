namespace SeeJit.Tests.Disassembling
{
    using System.IO;
    using SeeJit.Disassembling;
    using Xunit;

    public class AddressPrinterTests
    {
        [Theory]
        [InlineData(0, "00000000")]
        [InlineData(1, "00000001")]
        [InlineData(uint.MaxValue, "ffffffff")]
        public void Short(ulong address, string expected)
        {
            Assert.Equal(expected, Print(AddressPrinter.Short, address));
        }

        [Theory]
        [InlineData(0, "00000000`00000000")]
        [InlineData(1, "00000000`00000001")]
        [InlineData(uint.MaxValue, "00000000`ffffffff")]
        [InlineData((ulong)1 + uint.MaxValue, "00000001`00000000")]
        [InlineData(ulong.MaxValue, "ffffffff`ffffffff")]
        public void Long(ulong address, string expected)
        {
            Assert.Equal(expected, Print(AddressPrinter.Long, address));
        }

        private static string Print(AddressPrinter printer, ulong address)
        {
            var buffer = new StringWriter();
            printer.Print(buffer, address);
            return buffer.ToString();
        }
    }
}