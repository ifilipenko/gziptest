using System.Collections.Generic;

namespace Parallel.Compression.Cli.ParamsParsing
{
    internal class Commands
    {
        private const string InputFileArgDescription = "File to compress (required)";
        private const string OutputFileArgDescription = "Result file path (required)";

        public static readonly CommandArgument Compress =
            new CommandArgument("compress", "Compress file with blocking-gzip")
                .AddPositionParameters(Arguments.InputFile, InputFileArgDescription)
                .AddPositionParameters(Arguments.OutputFile, OutputFileArgDescription)
                .AddOption(Arguments.CpuThreads)
                .AddOption(Arguments.ReadBuffer);

        public static readonly CommandArgument Decompress
            = new CommandArgument("decompress", "Decompress file with blocking-gzip")
                .AddPositionParameters(Arguments.InputFile, InputFileArgDescription)
                .AddPositionParameters(Arguments.OutputFile, OutputFileArgDescription)
                .AddOption(Arguments.CpuThreads)
                .AddOption(Arguments.ReadBuffer);

        public static readonly CommandArgument Help = new CommandArgument("?", "Help");

        public static IEnumerable<CommandArgument> All
        {
            get
            {
                yield return Commands.Compress;
                yield return Commands.Decompress;
                yield return Commands.Help;
            }
        }
    }
}