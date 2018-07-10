using System;
using System.Diagnostics;
using Parallel.Compression.Cli.Ioc;
using Parallel.Compression.Cli.Output;
using Parallel.Compression.Configuration;
using Parallel.Compression.Errors;
using Parallel.Compression.Func;
using Parallel.Compression.Logging;

namespace Parallel.Compression.Cli.Commands
{
    internal abstract class AbstactCompressionCommand : ICommand
    {
        private readonly string inputFilePath;
        private readonly string outputFilePath;
        private readonly CompressorSettings settings;

        protected AbstactCompressionCommand(string inputFilePath, string outputFilePath, CompressorSettings settings)
        {
            this.inputFilePath = inputFilePath;
            this.outputFilePath = outputFilePath;
            this.settings = settings;
        }

        public Result Execute(ILog log, Dependencies dependencies)
        {
            var fileCompression = dependencies.GetFileCompression(settings);
            if (StartMessage != null)
            {
                Print.Info(StartMessage);
            }
            Print.Info(Messages.Parameters(settings));
            
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var (ratio, compressionError) = Execute(fileCompression, inputFilePath, outputFilePath);

            if (compressionError.HasValue)
            {
                return Messages.Error(compressionError.Value);
            }

            Print.Success(SuccessMessage(ratio, stopwatch.Elapsed));
            return Result.Successful();
        }

        protected abstract string SuccessMessage(int ratio, TimeSpan elapsed);

        protected abstract Result<int, ErrorCodes?> Execute(
            FileCompression compression,
            string inputFile,
            string outputFile);


        protected abstract string StartMessage { get; }
    }
}