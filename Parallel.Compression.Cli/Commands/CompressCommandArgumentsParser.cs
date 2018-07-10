using Parallel.Compression.Cli.ParamsParsing;
using Parallel.Compression.Func;

namespace Parallel.Compression.Cli.Commands
{
    internal static class CompressCommandArgumentsParser
    {
        public static Result<CompressionParameters> ToCompressionParameters(this CommandArgumentValues argumentValues)
        {
            var inputFile = argumentValues[Arguments.InputFile];
            var outputFile = argumentValues[Arguments.OutputFile];

            var (threadsPerCpu, threadsPerCpuError) = argumentValues.GetOptionAsInt(Arguments.CpuThreads);
            if (threadsPerCpuError != null)
            {
                return threadsPerCpuError;
            }
            
            var (bufferSize, bufferSizeError) = argumentValues.GetOptionAsInt(Arguments.ReadBuffer);
            if (bufferSizeError != null)
            {
                return bufferSizeError;
            }

            return new CompressionParameters(threadsPerCpu, bufferSize, inputFile, outputFile);
        }
    }
}