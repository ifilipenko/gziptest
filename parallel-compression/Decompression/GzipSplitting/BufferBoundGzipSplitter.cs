using System;
using System.Collections.Generic;
using System.IO;
using JetBrains.Annotations;
using Parallel.Compression.Decompression.Streams;
using Parallel.Compression.GzipFormat;
using Parallel.Compression.Helpers;
using Parallel.Compression.Logging;

namespace Parallel.Compression.Decompression.GzipSplitting
{
    internal class BufferBoundGzipSplitter : IGzipToIndependentBlockSplitter
    {
        private readonly ILog log;
        private readonly int bufferSize;

        public BufferBoundGzipSplitter(int bufferSize, [NotNull] ILog log)
        {
            if (bufferSize <= GzipHeader.Length)
                throw new ArgumentOutOfRangeException(nameof(bufferSize), bufferSize, "Buffer should be greater than gzip header length");

            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.bufferSize = bufferSize;
        }

        public IEnumerable<SplitResult> SplitToIndependentBlocks(RewindableReadonlyStream stream)
        {
            if (stream.Length == 0)
                yield break;

            // todo: take left stream bytes or buffer size
            var buffer = new byte[bufferSize]; // todo: use array pool
            var endOfStream = stream.IsReadToTheEnd;
            while (!endOfStream)
            {
                int read;
                GzipBuffer currentGzipBuffer;
                (read, endOfStream, currentGzipBuffer) = ReadGzipBufferFromStream(stream, buffer);
                if (read == 0 && endOfStream)
                {
                    yield return GzipSplittingStatus.StreamIsEnd;
                    yield break;
                }

                if (currentGzipBuffer.IsEmpty || currentGzipBuffer.NoHeaders || !currentGzipBuffer.IsStartsWithCompressedBlock)
                {
                    stream.ReturnTailOfReadedBytes(buffer.TakeFirst(read));
                    yield return GzipSplittingStatus.WrongFormat;
                    yield break;
                }

                if (!endOfStream && !currentGzipBuffer.IsContainAtLestOneWholeBlock)
                {
                    log.Info("Current buffer is too small to read gzip blocks as independent blocks");
                    stream.ReturnTailOfReadedBytes(buffer.TakeFirst(read));
                    yield return GzipSplittingStatus.CantReadBlock;
                    yield break;
                }

                var headers = currentGzipBuffer.Headers;
                int countHeaders;
                int? offsetToReturnBufferTail;
                if (endOfStream)
                {
                    countHeaders = headers.Count;
                    offsetToReturnBufferTail = null;
                }
                else
                {
                    countHeaders = headers.Count - 1;
                    offsetToReturnBufferTail = headers[headers.Count - 1].Position;
                }

                for (var i = 0; i < countHeaders; i++)
                {
                    var from = headers[i].Position;
                    var to = i < headers.Count - 1 ? headers[i + 1].Position : read;
                    var arraySegment = new ArraySegment<byte>(buffer, from, to - from);
                    yield return new IndependentGzipBlock(CopyBlockToStream(arraySegment));
                }

                if (offsetToReturnBufferTail.HasValue)
                {
                    stream.ReturnTailOfReadedBytes(new ArraySegment<byte>(buffer, offsetToReturnBufferTail.Value, read - offsetToReturnBufferTail.Value));
                }
            }

            yield return GzipSplittingStatus.StreamIsEnd;
        }

        private static (int Read, bool StreamIsEnd, GzipBuffer gzipBuffer) ReadGzipBufferFromStream(RewindableReadonlyStream stream, byte[] currentBuffer)
        {
            GzipBuffer gzipBuffer = default;

            var (readBuffer, endOfStream) = stream.ReadExactBuffer(currentBuffer);
            if (readBuffer.Count > 0)
            {
                var readGzipBuffer = new GzipBuffer(readBuffer);
                if (readGzipBuffer.NoHeaders)
                {
                    gzipBuffer = default;
                }
                else
                {
                    gzipBuffer = readGzipBuffer;
                    if (!endOfStream && readGzipBuffer.IsStartsWithCompressedBlock && readGzipBuffer.Headers.Count == 1)
                    {
                        endOfStream = stream.IsReadToTheEnd;
                    }
                }
            }

            return (readBuffer.Count, endOfStream, gzipBuffer);
        }

        private static MemoryStream CopyBlockToStream(ArraySegment<byte> bytes)
        {
            if (bytes.Array == null)
                throw new ArgumentException("Block can't be default", nameof(bytes));

            var stream = new MemoryStream(bytes.Count);
            stream.Write(bytes.Array, bytes.Offset, bytes.Count);
            stream.Position = 0;
            return stream;
        }
    }
}