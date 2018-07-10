using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Parallel.Compression.Tests.Helpers
{
    internal static class BytesHelpers
    {
        public static byte[] JoinIntoByteArray(this IEnumerable<ArraySegment<byte>> byteArrays)
        {
            using (var memoryStream = new MemoryStream())
            {
                foreach (var bytes in byteArrays)
                {
                    memoryStream.Write(bytes);
                }

                return memoryStream.ToArray();
            }
        }

        public static byte[] JoinAll(this IEnumerable<byte[]> byteArrays)
        {
            using (var memoryStream = new MemoryStream())
            {
                foreach (var bytes in byteArrays)
                {
                    memoryStream.Write(bytes);
                }

                return memoryStream.ToArray();
            }
        }

        public static MemoryStream AsStream(this ArraySegment<byte> bytes)
        {
            return bytes.ToArray().AsStream();
        }

        public static MemoryStream AsStream(this byte[] bytes)
        {
            var memoryStream = new MemoryStream();
            memoryStream.Write(bytes);
            memoryStream.Position = 0;
            return memoryStream;
        }

        public static string AsString(this ArraySegment<byte> bytes)
        {
            return Encoding.UTF8.GetString(bytes.Array, bytes.Offset, bytes.Count);
        }

        public static string AsString(this byte[] bytes)
        {
            return Encoding.UTF8.GetString(bytes);
        }

        public static ArraySegment<T> Fill<T>(this ArraySegment<T> segment, int from, T value)
        {
            var lastOffset = segment.Offset + segment.Count - 1;
            for (var i = from; i <= lastOffset; i++)
            {
                segment.Array[i] = value;
            }

            return segment;
        }
    }
}