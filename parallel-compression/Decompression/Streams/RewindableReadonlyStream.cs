using System;
using System.IO;
using Parallel.Compression.Helpers;

namespace Parallel.Compression.Decompression.Streams
{
    internal class RewindableReadonlyStream : Stream
    {
        private readonly Stream stream;
        private ArraySegment<byte> returnedBuffer;
        private long position;
        private bool someBufferLeft;

        public RewindableReadonlyStream(Stream stream)
        {
            this.stream = stream;
            position = stream.Position;
        }

        public ArraySegment<byte> ReturnedBuffer => returnedBuffer;

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset), "Need non negative number");
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), "Need non negative number");
            if (buffer.Length - offset < count)
                throw new ArgumentException("Invalid offset lenght");

            var currentReturnedBuffer = returnedBuffer;
            if (currentReturnedBuffer.Array != null)
            {
                var readCount = Math.Min(currentReturnedBuffer.Count, count);
                Buffer.BlockCopy(
                    currentReturnedBuffer.Array,
                    currentReturnedBuffer.Offset,
                    buffer,
                    offset,
                    readCount);

                var leftBufferCount = currentReturnedBuffer.Count - readCount;
                returnedBuffer = leftBufferCount > 0
                    ? new ArraySegment<byte>(currentReturnedBuffer.Array, currentReturnedBuffer.Offset + readCount, leftBufferCount)
                    : default;
                someBufferLeft = returnedBuffer.Count > 0;

                position += readCount;
                return readCount;
            }

            var read = stream.Read(buffer, offset, count);
            position += read;
            return read;
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
        public bool IsReadToTheEnd => position == stream.Length;

        public override void Flush()
        {
        }

        public void ReturnTailOfReadedBytes(ArraySegment<byte> bytes)
        {
            if (someBufferLeft)
            {
                // todo: test it
                throw new InvalidOperationException("Before return any buffer need to read it to the end");
            }
            
            var newPosition = position - bytes.Count;
            if (newPosition < 0)
                throw new ArgumentException("Bytes is too long", nameof(bytes));

            position = newPosition;
            returnedBuffer = returnedBuffer.Array == null
                ? bytes.Copy()
                : returnedBuffer.AppendSegment(bytes);
        }
    }
}