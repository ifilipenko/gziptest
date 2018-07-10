using System.IO;
using BenchmarkDotNet.Attributes;
using Parallel.Compression.BenchmarkTests.Files;
using Parallel.Compression.Compression;
using Parallel.Compression.Configuration;
using Parallel.Compression.Helpers;
using Parallel.Compression.Logging;

namespace Parallel.Compression.BenchmarkTests.Benchmarks
{
    [MemoryDiagnoser]
    public class CompressorBenchmark
    {
        private StreamCompressor streamCompressor;
        private const string TargetFile = "compressed.gz";
        private const string InputFile = "input.txt";

        [Params(85*1024, 1024*1024, 4*1024*1024)]
        public int InputFileBuffersSize { get; set; }
        
        [Params(1, 2, 8)]
        public int ThreadsPerCpu { get; set; }
        
        [Params(null, 10)]
        public int? CompressQueueSize { get; set; }

        [GlobalSetup]
        public void CreateTestFile()
        {
            FileUtils.DeleteIfExists(InputFile);
            var fileGenerator = new RandomFileGenerator();
            fileGenerator.GenerateTextFile(InputFile, 500.Megabytes());
        }

        [GlobalCleanup]
        public void DeleteTestFile()
        {
            FileUtils.DeleteIfExists(InputFile);
        }

        [IterationSetup]
        public void CreateCompressor()
        {
            FileUtils.DeleteIfExists(TargetFile);

            var settings = new CompressorSettingsBuilder()
                .SetDefaultOffsetLabel()
                .SetInputFileReadingBufferSize(InputFileBuffersSize)
                .SetParallelismByThreadsPerCpu(ThreadsPerCpu, CompressQueueSize)
                .GetSettings();
            streamCompressor = new StreamCompressor(new GzipBlockCompression(), settings, new StubLog());
        }

        [IterationCleanup]
        public void DeleteCompressedFile()
        {   
            FileUtils.DeleteIfExists(TargetFile);
        }

        [Benchmark]
        public int Compress()
        {
            using (var inputStream = File.OpenRead(InputFile))
            using (var outputStream = File.Open(TargetFile, FileMode.OpenOrCreate, FileAccess.ReadWrite))
            {
                var (ratio, _) = streamCompressor.Compress(inputStream, outputStream);
                return ratio;
            }
        }
    }
}