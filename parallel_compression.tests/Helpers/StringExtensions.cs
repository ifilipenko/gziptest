using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Parallel.Compression.Helpers;

namespace Parallel.Compression.Tests.Helpers
{
    internal static class StringExtensions
    {
        public static string JoinStrings(this IEnumerable<string> value)
        {
            return string.Join(string.Empty, value);
        }

        public static ArraySegment<byte> ToArraySegment(this string value)
        {
            return new ArraySegment<byte>(Encoding.UTF8.GetBytes(value));
        }

        public static byte[] ToBytes(this string value)
        {
            return Encoding.UTF8.GetBytes(value);
        }

        public static MemoryStream AsStream(this string value)
        {
            var stream = new MemoryStream();

            using (var streamWriter = new StreamWriter(stream, Encoding.UTF8, 1.Kilobytes(), true))
            {
                streamWriter.Write(value);
            }

            stream.Position = 0;

            return stream;
        }
    }
}