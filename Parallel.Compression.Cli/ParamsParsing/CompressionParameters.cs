using System;
using Parallel.Compression.Configuration;
using Parallel.Compression.Func;

namespace Parallel.Compression.Cli.ParamsParsing
{
    internal class CompressionParameters
    {
        private readonly int? threadsPerCpu;
        private readonly int? bufferSize;

        public CompressionParameters(int? threadsPerCpu, int? bufferSize, string inputFilePath, string outputFilePath)
        {
            this.threadsPerCpu = threadsPerCpu;
            this.bufferSize = bufferSize;
            InputFilePath = inputFilePath;
            OutputFilePath = outputFilePath;
        }

        public string InputFilePath { get; }
        public string OutputFilePath { get; }

        public Result<CompressorSettings> GetCompressorSettings()
        {
            var settingsBuilder = new CompressorSettingsBuilder()
                .SetDefaultOffsetLabel();

            try
            {
                if (bufferSize.HasValue)
                {
                    settingsBuilder.SetInputFileReadingBufferSize(bufferSize.Value);
                }
                else
                {
                    settingsBuilder.SetDefaultInputFileReadingBufferSize();
                }

                if (threadsPerCpu.HasValue)
                {
                    settingsBuilder.SetParallelismByThreadsPerCpu(threadsPerCpu.Value);
                }
                else
                {
                    settingsBuilder.SetDefaultPararllelism();
                }
            }
            catch (ArgumentException ex)
            {
                return ex.Message;
            }

            return settingsBuilder.GetSettings();
        }
    }
}