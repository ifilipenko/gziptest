using System;
using System.IO;
using JetBrains.Annotations;
using Parallel.Compression.GzipFormat;
using Parallel.Compression.Helpers;

namespace Parallel.Compression.Decompression.Streams
{
    internal class ExactlyPositionedAccoringToGzipBlockBoundsStream : Stream
    {
        private readonly RewindableReadonlyStream stream;
        private long position;
        private bool streamEnded;
        private bool isFirstTimeRead = true;
        private GzipBuffer leftBuffer;

        public ExactlyPositionedAccoringToGzipBlockBoundsStream([NotNull] RewindableReadonlyStream stream)
        {
            this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
            position = stream.Position;
        }

        public bool IsEndOfStream => streamEnded && leftBuffer.IsEmpty;

        public override int Read(byte[] buffer, int offset, int count)
        {
            CheckStreamPosition();
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset), "Need non negative number");
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), "Need non negative number");
            if (buffer.Length - offset < count)
                throw new ArgumentException($"Buffer have smaller elements {buffer.Length - offset} from given offset {offset} than requested in count {count}", nameof(count));

            if (count == 0)
                throw new ArgumentException("Requested zero elements", nameof(count));

            if (count < GzipHeader.Length)
                throw new ArgumentException("Buffer element count can't be less than Gzip header length", nameof(count));

            if (buffer.Length - offset == 0)
                throw new ArgumentException("Buffer has no elements from requested offset", nameof(offset));

            var currentGzipBuffer = leftBuffer;
            var isEnd = streamEnded;
            var read = 0;
            if (currentGzipBuffer.IsEmpty && !streamEnded)
            {
                (read, isEnd, currentGzipBuffer) = ReadGzipBufferFromStream();
            }

            if (!currentGzipBuffer.IsEmpty)
            {
                ArraySegment<byte> readBlock;
                (readBlock, currentGzipBuffer) = ReadFirstBockFromBuffer(currentGzipBuffer, count);
                read = readBlock.Count;

                if (readBlock.Array != null && readBlock.Count > 0)
                {
                    Buffer.BlockCopy(readBlock.Array, readBlock.Offset, buffer, offset, readBlock.Count);
                }
            }

            leftBuffer = currentGzipBuffer;
            position += read;
            streamEnded = isEnd;

            return read;

            (int Read, bool StreamIsEnd, GzipBuffer gzipBuffer) ReadGzipBufferFromStream()
            {
                GzipBuffer gzipBuffer = default;

                var (readBuffer, endOfStream) = stream.ReadExactBuffer(buffer, offset, count);
                if (readBuffer.Count > 0)
                {
                    var readGzipBuffer = new GzipBuffer(readBuffer);
                    gzipBuffer = readGzipBuffer.NoHeadersOrParts || readGzipBuffer.ContainsOnlyOneCompressedBlockFromStart
                        ? default
                        : readGzipBuffer.ToOwnedBuffer();
                }

                return (readBuffer.Count, endOfStream, gzipBuffer);
            }

            (ArraySegment<byte> Block, GzipBuffer LeftGzipBuffer) ReadFirstBockFromBuffer(GzipBuffer gzipBuffer, int requestedCount)
            {
                var (block, left) = gzipBuffer.CutFirstBlock();
                if (block.Array == null || block.Count == 0)
                {
                    if (left.ContainsOnlyPart)
                    {
                        block = gzipBuffer.GetPossiblePart();
                    }

                    left = default;
                }
                else if (block.Count > requestedCount)
                {
                    block = block.Slice(block.Offset, requestedCount);
                    var returnBlock = block.Slice(block.Offset + requestedCount, block.Count - requestedCount);
                    left = left.ReturnToStart(returnBlock);
                }

                return (block, left);
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override bool CanRead => stream.CanRead;
        public override bool CanSeek { get; } = false;
        public override bool CanWrite { get; } = false;
        public override long Length => stream.Length;

        public override long Position
        {
            get => position;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        private void CheckStreamPosition()
        {
            if (isFirstTimeRead)
            {
                if (position != stream.Position)
                    throw new InvalidOperationException("Stream was read since create wrapper. Maybe same thread chnaged in another stream.");

                isFirstTimeRead = false;
            }
        }
    }
}