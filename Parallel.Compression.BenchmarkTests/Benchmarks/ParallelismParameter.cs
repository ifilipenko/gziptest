using BenchmarkDotNet.Code;

namespace Parallel.Compression.BenchmarkTests.Benchmarks
{
    public class ParallelismParameter : IParam
    {
        public int CompressingQueueSize { get; set; }
        public int ParallelismLevel { get; set; }

        public string ToSourceCode()
        {
            return null;
        }

        public object Value => new object();
        public string DisplayText => ToString();
        //
        // public static implicit operator ParallelismParameter((ParallelismSettings, string) pair)
        // {
        //     return new ParallelismParameter(pair.Item1, pair.Item2);
        // }
    }
}