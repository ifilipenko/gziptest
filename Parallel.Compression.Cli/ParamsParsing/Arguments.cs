using System;

namespace Parallel.Compression.Cli.ParamsParsing
{
    internal static class Arguments
    {
        public const string InputFile = "inputFilePath";
        public const string OutputFile = "outputFilePath";

        private static readonly string CpuThreadsArgDescription =
            $"Threads per each logical core (the CPU have {Environment.ProcessorCount} cores)";

        private const string ReadBufferArgDescription = "Read buffer size";

        public static readonly CommandKeyOption CpuThreads =
            new CommandKeyOption(null, "--cputhreads", CpuThreadsArgDescription);

        public static readonly CommandKeyOption ReadBuffer =
            new CommandKeyOption(null, "--readbuffer", ReadBufferArgDescription);
    }
}