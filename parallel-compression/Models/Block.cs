using System;

namespace Parallel.Compression.Models
{
    public struct Block
    {
        private static readonly ArraySegment<byte> EmptyBytes = new ArraySegment<byte>(new byte[0]);
        private readonly ArraySegment<byte>? bytes;

        public Block(ArraySegment<byte> bytes, long offset)
        {
            if (offset <= 0)
                throw new ArgumentException("Offset can't be negative", nameof(offset));

            this.bytes = bytes.Array == null ? EmptyBytes : bytes;
            Offset = offset;
        }

        public ArraySegment<byte> Bytes => bytes ?? EmptyBytes;
        public long Offset { get; }
        public bool IsEmpty => Bytes.Count == 0;
    }
}