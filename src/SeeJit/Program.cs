namespace SeeJit
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Runtime;
    using CommandLine;

    class Program
    {
        static void Main(string[] args)
        {
            ProfileOptimization.SetProfileRoot(Path.GetDirectoryName(typeof(Program).Assembly.Location));
            ProfileOptimization.StartProfile("Startup.Profile");

            var parseResult = Parser.Default.ParseArguments<Options>(args);
            if (parseResult.Errors.Any())
                return;

            var options = parseResult.Value;

            var fileOptions = new DisassembleFileOptions
            {
                FilePath = options.FilePath,
                DisableOptimization = options.DisableOptimization,
                IsVerbose = options.IsVerbose
            };

            try
            {
                SeeJitHelper.DisassembleFile(fileOptions, Console.Out);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Unhandled exception: " + ex.GetType());
                Console.Error.WriteLine(ex.Message);
                Console.Error.WriteLine(ex.StackTrace);
            }
        }
    }

    internal class Options
    {
        [Option('f', "file", Required = true, HelpText = "Source file to be compiled and disassembled.")]
        public string FilePath { get; set; }

        [Option('d', "disable-optimization", HelpText = "Disable optimization when compiling source file.")]
        public bool DisableOptimization { get; set; }

        [Option('v', "verbose", HelpText = "Print verbose messages.")]
        public bool IsVerbose { get; set; }
    }
}