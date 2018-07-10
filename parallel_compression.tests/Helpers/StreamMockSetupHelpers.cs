using System;
using System.IO;
using NSubstitute;

namespace Parallel.Compression.Tests.Helpers
{
    public enum StreamOps
    {
        Seek,
        SetLength,
        Write,
        Read
    }

    internal static class StreamMockSetupHelpers
    {
        public static Stream ThrowsExceptionOnWrite(this Stream stream, Exception exception)
        {
            return stream.ThrowsExceptionOn(StreamOps.Write, exception);
        }


        public static Stream ThrowsExceptionOn(this Stream stream, StreamOps operation, Exception exception)
        {
            switch (operation)
            {
                case StreamOps.SetLength:
                    stream.When(x => x.SetLength(Any.Long)).Do(_ => throw exception);
                    break;
                case StreamOps.Write:
                    stream.When(x => x.Write(Any.Buffer, Any.Offset, Any.Count)).Do(_ => throw exception);
                    break;
                case StreamOps.Read:
                    stream.When(x => x.Read(Any.Buffer, Any.Offset, Any.Count)).Do(_ => throw exception);
                    break;
                case StreamOps.Seek:
                    stream.When(x => x.Seek(Any.Long, Any.Origin)).Do(_ => throw exception);
                    break;
            }
            
            return stream;
        }
    }
}