using System;
using System.IO;
using Parallel.Compression.Func;

namespace Parallel.Compression.Helpers
{
    internal static class StreamHelpers
    {
        // todo: replace this with ReadExactBuffer
        public static Result ReadExactFullBuffer(this Stream stream, byte[] buffer)
        {
            var offset = 0;
            var count = buffer.Length;
            
            while (offset < count)
            {
                var read = stream.Read(buffer, offset, count - offset);
                if (read == 0)
                    return "End of stream";
                offset += read;
            }

            return Result.Successful();
        }
        
        public static (ArraySegment<byte> Buffer, bool StreamIsEnd) ReadExactFullBuffer(this Stream stream, int bufferSize)
        {
            var buffer = new byte[bufferSize];
            return stream.ReadExactBuffer(buffer);
        }
        
        public static (ArraySegment<byte> Buffer, bool StreamIsEnd) ReadExactBuffer(this Stream stream, byte[] buffer)
        {
            var offset = 0;
            var streamIsEnd = false;
            var bufferSize = buffer.Length;
            
            while (offset < bufferSize)
            {
                var read = stream.Read(buffer, offset, bufferSize - offset);
                if (read == 0)
                {
                    streamIsEnd = true;
                    break;
                }
                offset += read;
            }

            return (new ArraySegment<byte>(buffer, 0, offset), streamIsEnd);
        }
        
        public static (ArraySegment<byte> Buffer, bool StreamIsEnd) ReadExactBuffer(this Stream stream, byte[] buffer, int offset, int count)
        {
            var streamIsEnd = false;
            var currentOffset = offset;
            
            while (count > 0)
            {
                var read = stream.Read(buffer, currentOffset, count);
                if (read == 0)
                {
                    streamIsEnd = true;
                    break;
                }
                count -= read;
                currentOffset += read;
            }

            return (new ArraySegment<byte>(buffer, offset, currentOffset - offset), streamIsEnd);
        }
        
        public static (int read, bool StreamIsEnd) ReadExactFullBuffer(this Stream stream, byte[] buffer, int offset, int count)
        {
            while (offset < count)
            {
                var read = stream.Read(buffer, offset, count - offset);
                if (read == 0)
                    break;
                offset += read;
            }

            return (offset, offset < count);
        }
    }
}
