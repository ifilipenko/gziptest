using System;
using System.IO;

namespace Parallel.Compression.Tests.FakeStreams
{
    internal class NotReadableStream : Stream
    {
        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException("Read is not supported");
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return 0;
        }

        public override void SetLength(long value)
        {
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
        }

        public override bool CanRead { get; } = false;
        public override bool CanSeek { get; } = true;
        public override bool CanWrite { get; } = true;
        public override long Length { get; } = 0;
        public override long Position { get; set; } = 0;
    }

    internal class NotSeekableStream : Stream
    {
        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return 0;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException("Seek operation is not supported");
        }

        public override void SetLength(long value)
        {
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
        }

        public override bool CanRead { get; } = true;
        public override bool CanSeek { get; } = false;
        public override bool CanWrite { get; } = true;
        public override long Length { get; } = 0;
        public override long Position { get; set; } = 0;
    }
}