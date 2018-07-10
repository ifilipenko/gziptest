using System;
using System.Collections.Generic;
using System.IO;
using JetBrains.Annotations;
using Parallel.Compression.Decompression.Streams;
using Parallel.Compression.Func;
using Parallel.Compression.GzipFormat;
using Parallel.Compression.Helpers;
using Parallel.Compression.Logging;

namespace Parallel.Compression.Decompression.GzipSplitting
{
    internal class MimeTypeLengthGzipSplitter : IGzipToIndependentBlockSplitter
    {
        private readonly int blockSizeLimit;
        private readonly ILog log;

        public MimeTypeLengthGzipSplitter(int blockSizeLimit, [NotNull] ILog log)
        {
            if (blockSizeLimit < 1)
                throw new ArgumentOutOfRangeException(nameof(blockSizeLimit), blockSizeLimit, "Invalid block size limit");
            this.blockSizeLimit = blockSizeLimit;
            this.log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public IEnumerable<SplitResult> SplitToIndependentBlocks(RewindableReadonlyStream inputStream)
        {
            long GetCurrentHeaderPosition(ArraySegment<byte> headerBytes)
            {
                return inputStream.Position - headerBytes.Offset;
            }
            
            Result<int, GzipSplittingStatus?> GetBlockLengthFromGzipHeader(ArraySegment<byte> headerBytes)
            {
                var header = GzipHeader.FindFirst(headerBytes);
                if (header == null)
                {
                    return GzipSplittingStatus.WrongFormat;
                }

                var length = header.Value.GetMimetypeAsInt();
                if (length == 0)
                {
                    return GzipSplittingStatus.CantReadBlock;
                }

                if (length > blockSizeLimit)
                {
                    log.Info($"Block size {length} at offset {GetCurrentHeaderPosition(headerBytes)} is greater than limit {blockSizeLimit}");
                    return GzipSplittingStatus.CantReadBlock;
                }

                return length;
            }

            (int length, ArraySegment<byte> readBytes, bool endOfStream, GzipSplittingStatus? error) ReadCurrentGzipBlockLengthFromStream()
            {
                var length = 0;
                GzipSplittingStatus? readLengthError = null;
                
                var bytes = new byte[GzipHeader.Length];
                var (headerBytes, isEndOfStream) = inputStream.ReadExactBuffer(bytes);
                if (headerBytes.Count < bytes.Length)
                {
                    if (!isEndOfStream)
                    {
                        readLengthError = GzipSplittingStatus.WrongFormat;
                    }
                }
                else
                {
                    (length, readLengthError) = GetBlockLengthFromGzipHeader(headerBytes);
                }

                return (length, headerBytes, isEndOfStream, readLengthError);
            }

            var currentBlockPoistion = inputStream.Position;
            var (blockLength, headerReadBytes, endOfStream, error) = ReadCurrentGzipBlockLengthFromStream();
            if (headerReadBytes.Count > 0)
            {
                inputStream.ReturnTailOfReadedBytes(headerReadBytes);
            }
            if (error.HasValue)
            {
                yield return error.Value;
                yield break;
            }

            while (!endOfStream)
            {
                ArraySegment<byte> buffer;
                var blockAndNextHeaderLength = blockLength + GzipHeader.Length;
                (buffer, endOfStream) = inputStream.ReadExactFullBuffer(blockAndNextHeaderLength);
                if (!endOfStream && buffer.Count == 0)
                {
                    log.Info($"Block hader at offset {currentBlockPoistion} contain invalid length or non length in mime type");
                    yield return GzipSplittingStatus.CantReadBlock;
                    yield break;
                }
                currentBlockPoistion = inputStream.Position;

                ArraySegment<byte> independentBlockBytes;
                var nextBlockLength = 0;
                GzipSplittingStatus? nextHeaderError = null;
                if (buffer.Count == blockLength && endOfStream)
                {
                    independentBlockBytes = buffer;
                }
                else if (buffer.Count == blockAndNextHeaderLength)
                {
                    var headerBytes = buffer.SliceFromEnd(GzipHeader.Length);
                    (nextBlockLength, nextHeaderError) = GetBlockLengthFromGzipHeader(headerBytes);
                    if (nextHeaderError != GzipSplittingStatus.WrongFormat)
                    {
                        inputStream.ReturnTailOfReadedBytes(headerBytes);
                        independentBlockBytes = buffer.RemoveFromEnd(GzipHeader.Length);
                    }
                }
                
                if (independentBlockBytes.Count == 0)
                {
                    inputStream.ReturnTailOfReadedBytes(buffer);
                    yield return GzipSplittingStatus.CantReadBlock;
                    yield break;
                }

                yield return new IndependentGzipBlock(WrapBufferWithStream(independentBlockBytes));

                if (nextHeaderError.HasValue)
                {
                    yield return nextHeaderError.Value;
                    yield break;
                }

                blockLength = nextBlockLength;
            }

            yield return GzipSplittingStatus.StreamIsEnd;
        }

        private MemoryStream WrapBufferWithStream(ArraySegment<byte> bytes)
        {
            if (bytes.Array == null)
                throw new ArgumentException("Block can't be default", nameof(bytes));

            return new MemoryStream(bytes.Array, bytes.Offset, bytes.Count, false);
        }
    }
}