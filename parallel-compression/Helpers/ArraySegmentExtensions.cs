using System;
using Parallel.Compression.Models;

namespace Parallel.Compression.Helpers
{
    internal static class ArraySegmentExtensions
    {
        private static readonly byte[] EmptyBytes = new byte[0];

        public static int LastOffset<T>(this ArraySegment<T> value)
        {
            return value.Offset + value.Count - 1;
        }
        
        public static ArraySegment<T> TakeFirst<T>(this ArraySegment<T> value, int count)
        {
            return value.Array == null
                ? value
                : new ArraySegment<T>(value.Array, value.Offset, count);
        }

        public static ArraySegment<T> ShiftOffsetRight<T>(this ArraySegment<T> value, int skip)
        {
            if (skip == 0)
                return value;
            var array = value.Array ?? Empty<T>.Array;
            return new ArraySegment<T>(array, value.Offset + skip, value.Count - skip);
        }

        public static ArraySegment<T> SliceFromEnd<T>(this ArraySegment<T> value, int count)
        {
            if (count == 0)
                return value;
            var array = value.Array ?? Empty<T>.Array;
            return new ArraySegment<T>(array, value.Offset + value.Count - count, count);
        }

        public static ArraySegment<T> RemoveFromEnd<T>(this ArraySegment<T> value, int count)
        {
            if (count == 0)
                return value;
            var array = value.Array ?? Empty<T>.Array;
            return new ArraySegment<T>(array, value.Offset, value.Count - count);
        }

        public static ArraySegment<T> Slice<T>(this ArraySegment<T> value, int skip, int count)
        {
            var array = value.Array ?? Empty<T>.Array;
            return new ArraySegment<T>(array, value.Offset + skip, count);
        }
        
        public static ArraySegment<T> SliceFromTheEnd<T>(this ArraySegment<T> value, int readFromEnd)
        {
            var array = value.Array ?? Empty<T>.Array;
            return new ArraySegment<T>(array, value.LastOffset() - readFromEnd + 1, readFromEnd);
        }

        public static ArraySegment<T> SetForwardOffset<T>(this ArraySegment<T> value, int newOffset)
        {
            var array = value.Array ?? Empty<T>.Array;
            if (value.Offset == newOffset)
                return value;
            if (newOffset < value.Offset)
                throw new ArgumentException("New offset can be less than current offset", nameof(newOffset));
            
            var count = value.Count - (newOffset - value.Offset);
            return new ArraySegment<T>(array, newOffset, count);
        }

        public static bool IsMatchToMask(this ArraySegment<byte> arraySegment, int startIndex, ByteMask[] mask)
        {
            var array = arraySegment.Array;
            if (array == null || mask.Length == 0 || arraySegment.Count - startIndex < mask.Length)
                return false;

            startIndex += arraySegment.Offset;
            for (var i = 0; i < mask.Length; i++)
            {
                var maskByte = mask[i];
                if (!maskByte.IsMatched(array[startIndex + i]))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool IsMatchedPartiallyFromEnd(this ArraySegment<byte> arraySegment, ByteMask[] mask, int count)
        {
            var array = arraySegment.Array;
            if (array == null || mask.Length == 0 || arraySegment.Count == 0)
                return false;
            if (arraySegment.Count < count)
                throw new ArgumentException("Count of matched element can't be greater than segment", nameof(count));
            if (mask.Length < count)
                throw new ArgumentException("Count of matched element can't be greater than mask size", nameof(count));

            var startIndex = arraySegment.Offset + arraySegment.Count - count;
            for (var i = 0; i < count; i++)
            {
                var maskByte = mask[i];
                if (!maskByte.IsMatched(array[startIndex + i]))
                {
                    return false;
                }
            }

            return true;
        }

        public static ArraySegment<byte> AppendSegment(this ArraySegment<byte> arraySegment, ArraySegment<byte> other)
        {
            if (other.Array == null || other.Count == 0)
                return arraySegment;
            if (arraySegment.Array == null || arraySegment.Count == 0)
                return other;

            var count = arraySegment.Count + other.Count;
            var joinedArray = new byte[count];

            Buffer.BlockCopy(arraySegment.Array, arraySegment.Offset, joinedArray, 0, arraySegment.Count);
            Buffer.BlockCopy(other.Array, other.Offset, joinedArray, arraySegment.Count, other.Count);

            return new ArraySegment<byte>(joinedArray, 0, joinedArray.Length);
        }

        public static ArraySegment<byte> Copy(this ArraySegment<byte> arraySegment)
        {
            var array = arraySegment.CopyToArray();
            return new ArraySegment<byte>(array, 0, array.Length);
        }

        public static byte[] CopyToArray(this ArraySegment<byte> arraySegment)
        {
            if (arraySegment.Array == null || arraySegment.Count == 0)
                return EmptyBytes;

            var array = new byte[arraySegment.Count];
            Buffer.BlockCopy(arraySegment.Array, arraySegment.Offset, array, 0, arraySegment.Count);
            return array;
        }
    }
}