using System;

namespace Parallel.Compression.Errors
{
    internal class CompressorException : Exception
    {
        public CompressorException(ErrorCodes error)
            : base("Error occured " + error)
        {
            Error = error;
        }
        
        public ErrorCodes Error { get; }
    }
}