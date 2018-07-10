using System;

namespace Parallel.Compression.Configuration
{
    public class CompressorSettingsBuilder
    {
        private const int InputStreamThreads = 1;
        private const int OutputStreamThreads = 1;
        private const int DefaultParallelismCpuMultiplier = 1;
        private const int LohThreashold = 84988;
        private const string DefaultOffsetLabel = "========";

        private int threadsCount;
        private int compressingQueueSize;
        private string offsetLabel;
        private int inputFileReadingBufferSize;

        public CompressorSettingsBuilder SetDefaultOffsetLabel()
        {
            offsetLabel = DefaultOffsetLabel;
            return this;
        }

        public CompressorSettingsBuilder SetOffsetLabel(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Invalid output file offset label", nameof(value));
            }

            offsetLabel = value;
            return this;
        }

        public CompressorSettingsBuilder SetDefaultPararllelism()
        {
            return SetParallelismByThreadsPerCpu(DefaultParallelismCpuMultiplier);
        }

        public CompressorSettingsBuilder SetParallelismByThreadsPerCpu(int threadsPerCpu, int? compressingQueueSize = null)
        {
            if (threadsPerCpu <= 0)
                throw new ArgumentException("CPU threads multyplier can't be negative or 0", nameof(threadsPerCpu));

            threadsCount = Environment.ProcessorCount*threadsPerCpu;
            if (compressingQueueSize.HasValue)
            {
                if (compressingQueueSize.Value <= 0)
                    throw new ArgumentException("Compression queue size can't be negative or 0", nameof(compressingQueueSize));
                this.compressingQueueSize = compressingQueueSize.Value;
            }
            else
            {
                this.compressingQueueSize = Math.Max(1, threadsCount - InputStreamThreads - OutputStreamThreads);
            }

            return this;
        }

        public CompressorSettingsBuilder SetInputFileReadingBufferSize(int bufferSize)
        {
            if (bufferSize <= 0)
                throw new ArgumentException("Input file buffer size can't be negative or 0", nameof(bufferSize));

            inputFileReadingBufferSize = bufferSize;
            return this;
        }

        public CompressorSettingsBuilder SetDefaultInputFileReadingBufferSize()
        {
            inputFileReadingBufferSize = LohThreashold;
            return this;
        }

        public CompressorSettings GetSettings()
        {
            if (threadsCount == 0 || compressingQueueSize == 0)
                throw new InvalidOperationException("Parallelism isn't set up before");
            if (inputFileReadingBufferSize == 0)
                throw new InvalidOperationException("Input file buffer isn't set up before");
            if (offsetLabel == null)
                throw new InvalidOperationException("Output file offset isn't set up, before");

            return new CompressorSettings(
                writeBlockLengthIntoMimetypeSection: true,
                inputFileReadingBufferSize: inputFileReadingBufferSize,
                threadsCount: threadsCount,
                compressingQueueSize: compressingQueueSize,
                offsetLabel: offsetLabel
            );
        }
    }
}