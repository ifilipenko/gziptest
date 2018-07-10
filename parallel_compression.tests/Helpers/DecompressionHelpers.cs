using System;
using System.IO;
using System.Text;

namespace Parallel.Compression.Tests.Helpers
{
    internal static class DecompressionHelpers
    {
        public static string DecompressToString(this ArraySegment<byte> bytes)
        {
            return bytes.ToArray().DecompressToString();
        }

        public static string DecompressToString(this byte[] bytes)
        {
            using (var compressedStream = new MemoryStream(bytes))
            {
                return DecompressToString(compressedStream);
            }
        }
        
        public static string DecompressToString(this MemoryStream compressedStream)
        {
            using (var uncompressedStream = new MemoryStream())
            {
                ICSharpCode.SharpZipLib.GZip.GZip.Decompress(compressedStream, uncompressedStream, false);

                uncompressedStream.Position = 0;
                using (var streamReader = new StreamReader(uncompressedStream, Encoding.UTF8))
                {
                    return streamReader.ReadToEnd();
                }
            }
        }
    }
}