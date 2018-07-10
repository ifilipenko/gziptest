using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Parallel.Compression.Tests.Helpers
{
    internal static class StreamHelpers
    {
        public static Stream WriteString(this Stream stream, string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            return stream.WriteBytes(bytes);
        }

        public static Stream WriteLong(this Stream stream, long value)
        {
            return stream.WriteBytes(BitConverter.GetBytes(value));
        }

        public static Stream WriteBytes(this Stream stream, byte[] bytes)
        {
            stream.Write(bytes, 0, bytes.Length);
            return stream;
        }

        public static IEnumerable<byte[]> SpitToBytes(this Stream stream, int bufferSize)
        {
            var buffer = new byte[bufferSize];
            int read;
            while ((read = stream.Read(buffer, 0, bufferSize)) > 0)
            {
                yield return new ArraySegment<byte>(buffer, 0, read).ToArray();
            }
        }

        public static TStream SeekToBegin<TStream>(this TStream stream)
            where TStream : Stream
        {
            stream.Position = 0;
            return stream;
        }

        public static string ReadToString(this MemoryStream stream)
        {
            using (var streamReader = new StreamReader(stream, Encoding.UTF8))
            {
                return streamReader.ReadToEnd();
            }
        }
    }
}