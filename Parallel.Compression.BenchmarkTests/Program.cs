using BenchmarkDotNet.Running;
using Parallel.Compression.BenchmarkTests.Benchmarks;

namespace Parallel.Compression.BenchmarkTests
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            BenchmarkRunner.Run<CompressorBenchmark>();
        }
    }
}