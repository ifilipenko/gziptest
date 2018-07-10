using System;

namespace Parallel.Compression.Helpers
{
    public static class BytesExtensions
    {
        public static bool UnsafeEquals(this byte[] bytes, byte[] other)
        {
            return Compare(bytes, 0, bytes.Length, other, 0, other.Length) == 0;
        }

        public static bool UnsafeEquals(this ArraySegment<byte> bytes, ArraySegment<byte> other)
        {
            return Compare(bytes.Array, bytes.Offset, bytes.Count, other.Array, other.Offset, other.Count) == 0;
        }
        
        public static bool UnsafeEquals(this ArraySegment<byte> bytes, byte[] other)
        {
            return Compare(bytes.Array, bytes.Offset, bytes.Count, other, 0, other.Length) == 0;
        }

        private static unsafe int Compare(byte[] bytes1, int offset1, int lenth1, byte[] bytes2, int offset2, int length2)
        {
            fixed (byte* p1 = bytes1, p2 = bytes2)
            {
                byte* x1 = p1, x2 = p2;
                var length = Math.Min(lenth1, length2);
                x1 += offset1;
                x2 += offset2;

                for (var i = 0; i < length / 8; i++, x1 += 8, x2 += 8)
                {
                    if (*(long*) x1 != *(long*) x2)
                    {
                        for (var j = 0; j < 8; j++, x1 += 1, x2 += 1)
                        {
                            if (*x1 != *x2)
                            {
                                return *x1 - *x2;
                            }
                        }
                    }
                }

                for (var i = 0; i < length % 8; i++, x1 += 1, x2 += 1)
                {
                    if (*x1 != *x2)
                    {
                        return *x1 - *x2;
                    }
                }

                return lenth1 - length2;
            }
        }
    }
}