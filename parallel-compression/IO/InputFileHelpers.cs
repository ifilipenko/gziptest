using System.Collections.Generic;
using Parallel.Compression.Errors;
using Parallel.Compression.Func;
using Parallel.Compression.Helpers;
using Parallel.Compression.Models;

namespace Parallel.Compression.IO
{
    internal static class InputFileHelpers
    {
        public static IEnumerable<Result<Block, ErrorCodes?>> ReadBlocks(this InputFile file, int bufferSize, long offset)
        {
            if (offset > 0)
            {
                var offsetError = file.SeekTo(offset);
                if (offsetError != null)
                {
                    yield return offsetError;
                    yield break;
                }
            }

            var lastReadOffset = offset;
            
            int readBytesCount;
            do
            {
                ErrorCodes? error;
                var readBuffer = new byte[bufferSize]; // todo: use ArrayPool and release aqcuired buffer when Block is processed
                (readBytesCount, error) = file.ReadTo(readBuffer);
                if (error.HasValue)
                {
                    yield return error.Value;
                    break;
                }

                lastReadOffset += readBytesCount;
                var readBytes = readBuffer.Slice(readBytesCount);

                yield return new Block(readBytes, lastReadOffset);
            } while (readBytesCount > 0);
        }
    }
}