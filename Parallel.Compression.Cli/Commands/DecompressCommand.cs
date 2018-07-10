using System;
using Parallel.Compression.Cli.Output;
using Parallel.Compression.Configuration;
using Parallel.Compression.Errors;
using Parallel.Compression.Func;

namespace Parallel.Compression.Cli.Commands
{
    internal class DecompressCommand : AbstactCompressionCommand
    {
        public DecompressCommand(
            string inputFilePath,
            string outputFilePath,
            CompressorSettings settings)
            : base(inputFilePath, outputFilePath, settings)
        {
        }

        protected override string SuccessMessage(int ratio, TimeSpan elapsed)
        {
            return Messages.SuccessfulDecompress(ratio, elapsed);
        }

        protected override Result<int, ErrorCodes?> Execute(FileCompression compression, string inputFile,
            string outputFile)
        {
            return compression.DecompressFile(inputFile, outputFile);
        }

        protected override string StartMessage => "Start decompression";
    }
}