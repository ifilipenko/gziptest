using System;
using System.Collections.Generic;

namespace Parallel.Compression.Helpers
{
    internal static class ByteArrayExtensions
    {   
        public static ArraySegment<byte> TakeFirst(this byte[] value, int count)
        {
            return new ArraySegment<byte>(value, 0, count);
        }
        
        public static IEnumerable<ArraySegment<byte>> Slices(this byte[] bytes, int size)
        {
            var leftBytes = bytes.Length;

            var offset = 0;
            while (leftBytes > 0)
            {
                var count = Math.Min(leftBytes, size);
                var segment = bytes.Segment(offset, count);
                yield return segment;
                
                leftBytes -= count;
                offset += count;
            }
        }

        // todo: replace with TakeFirst
        public static ArraySegment<byte> Slice(this byte[] bytes, int readFromStart)
        {
            return new ArraySegment<byte>(bytes, 0, readFromStart);
        }

        public static ArraySegment<byte> Segment(this byte[] bytes, int offset, int count)
        {
            return new ArraySegment<byte>(bytes, offset, count);
        }

        public static ArraySegment<byte> Segment(this byte[] bytes, int offset)
        {
            return new ArraySegment<byte>(bytes, offset, bytes.Length - offset);
        }

        public static ArraySegment<byte> ToSegment(this byte[] bytes)
        {
            return new ArraySegment<byte>(bytes, 0, bytes.Length);
        }

        public static ArraySegment<byte> SliceFromTheEnd(this byte[] bytes, int readFromEnd)
        {
            return new ArraySegment<byte>(bytes, bytes.Length - readFromEnd, readFromEnd);
        }
    }
}