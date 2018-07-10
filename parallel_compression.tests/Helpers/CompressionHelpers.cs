using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Parallel.Compression.Compression;
using Parallel.Compression.Models;

namespace Parallel.Compression.Tests.Helpers
{
    internal static class CompressionHelpers
    {
        public static IEnumerable<ArraySegment<byte>> CompressIntoBlocksWith(this string text, int readBufferSize, IBlockCompression blockCompression)
        {
            using (var memoryStream = text.AsStream())
            {
                foreach (var bytes in memoryStream.SpitToBytes(readBufferSize))
                {
                    var block = new Block(bytes, memoryStream.Position);
                    var compressed = blockCompression.Compress(block);
                    yield return compressed.Bytes;
                }
            }
        }

        public static byte[] CompessWithDecompressionHelp(this string text, DecompressionHelpMode mode)
        {
            return text.ToBytes().CompessWithDecompressionHelp(mode);
        }

        public static byte[] CompessWithDecompressionHelp(this byte[] value, DecompressionHelpMode mode)
        {
            var compression = new GzipBlockCompression();
            var compressed = compression.Compress(new Block(value, 1), mode);
            return compressed.Bytes.ToArray();
        }
        
        public static ArraySegment<byte> GzipCompress(this string text)
        {
            using (var memoryStream = new MemoryStream())
            {
                using (var gZipStream = new GZipStream(memoryStream, CompressionMode.Compress, true))
                {
                    gZipStream.Write(text.ToArraySegment());
                }

                return memoryStream.ToArray();
            }
        }
    }
}