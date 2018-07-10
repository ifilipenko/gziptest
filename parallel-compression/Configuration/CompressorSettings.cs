using System;
using Parallel.Compression.Helpers;

namespace Parallel.Compression.Configuration
{
    public class CompressorSettings
    {
        public CompressorSettings(
            int inputFileReadingBufferSize, 
            int threadsCount, 
            int compressingQueueSize, 
            string offsetLabel, bool writeBlockLengthIntoMimetypeSection)
        {
            if (inputFileReadingBufferSize <= 0)
                throw new ArgumentException("Invalid input file reading buffer size", nameof(inputFileReadingBufferSize));
            
            if (threadsCount <= 0)
                throw new ArgumentException("Invalid threads count", nameof(threadsCount));
            
            if (compressingQueueSize <= 0)
                throw new ArgumentException("Invalid compressing queue size", nameof(compressingQueueSize));
            
            if (string.IsNullOrWhiteSpace(offsetLabel))
                throw new ArgumentException("Invalid offset label", nameof(offsetLabel));
            
            InputFileReadingBufferSize = inputFileReadingBufferSize;
            ThreadsCount = threadsCount;
            CompressingQueueSize = compressingQueueSize;
            OffsetLabel = offsetLabel;
            WriteBlockLengthIntoMimetypeSection = writeBlockLengthIntoMimetypeSection;
        }
        
        public bool WriteBlockLengthIntoMimetypeSection { get; }
        public int InputFileReadingBufferSize { get; }
        public int ThreadsCount { get; }
        public int CompressingQueueSize { get; }
        public string OffsetLabel { get; }
        public int ParallelDecompressionBufferSize => InputFileReadingBufferSize;
        public int MaxParallelDecompressionBufferSize => 128.Megabytes();
    }
}