using System;
using Parallel.Compression.Cli.Output;
using Parallel.Compression.Configuration;
using Parallel.Compression.Errors;
using Parallel.Compression.Func;

namespace Parallel.Compression.Cli.Commands
{
    internal class CompressCommand : AbstactCompressionCommand
    {
        public CompressCommand(
            string inputFilePath,
            string outputFilePath,
            CompressorSettings settings)
            : base(inputFilePath, outputFilePath, settings)
        {
        }

        protected override string SuccessMessage(int ratio, TimeSpan elapsed)
        {
            return Messages.SuccessfulCompress(ratio, elapsed);
        }

        protected override Result<int, ErrorCodes?> Execute(FileCompression compression, string inputFile,
            string outputFile)
        {
            return compression.CompressFiles(inputFile, outputFile);
        }

        protected override string StartMessage => "Start compression";
    }
}