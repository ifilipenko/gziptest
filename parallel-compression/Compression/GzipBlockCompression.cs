using System;
using System.IO;
using System.IO.Compression;
using Parallel.Compression.GzipFormat;
using Parallel.Compression.Helpers;
using Parallel.Compression.Models;

namespace Parallel.Compression.Compression
{
    public class GzipBlockCompression : IBlockCompression
    {
        private static readonly int DecompressionBufferSize = 1.Kilobytes();
        private static readonly ArraySegment<byte> EmptyBytes = new ArraySegment<byte>(new byte[0]);

        public Block Compress(Block inputBlock, DecompressionHelpMode decompressionHelpMode)
        {
            if (inputBlock.Bytes.Count == 0)
                return new Block(EmptyBytes, inputBlock.Offset);

            using (var memoryStream = new MemoryStream())
            {
                using (var gZipStream = new GZipStream(memoryStream, CompressionMode.Compress, true))
                {
                    // ReSharper disable once AssignNullToNotNullAttribute
                    gZipStream.Write(inputBlock.Bytes.Array, inputBlock.Bytes.Offset, inputBlock.Bytes.Count);
                }

                var compressedBytes = memoryStream.ToArray();
                var buffer = new ArraySegment<byte>(compressedBytes, 0, (int) memoryStream.Position);

                if (decompressionHelpMode == DecompressionHelpMode.BlockLengthInMimetypeSection)
                {
                    GzipHeader.FindFirst(buffer)?.SetMimetypeBytes(BitConverter.GetBytes(buffer.Count));
                }

                return new Block(buffer, inputBlock.Offset);
            }
        }

        public Block Decompress(Block inputBlock)
        {
            if (inputBlock.Bytes.Count == 0)
                return new Block(EmptyBytes, inputBlock.Offset);

            using (var outputStream = new MemoryStream())
            using (var inputStream = ToStream(inputBlock))
            {
                using (var gZipStream = new GZipStream(inputStream, CompressionMode.Decompress, true))
                {
                    // todo: use ArrayPool here
                    var buffer = new byte[DecompressionBufferSize];
                    int read;
                    while ((read = gZipStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        outputStream.Write(buffer, 0, read);
                    }
                }

                var decompressedBytes = new ArraySegment<byte>(outputStream.ToArray(), 0, (int) outputStream.Position);
                return new Block(decompressedBytes, inputBlock.Offset);
            }

            Stream ToStream(Block block)
            {
                var bytesArray = block.Bytes.Array ?? throw new ArgumentException("Block can't be empty", nameof(block));
                return new MemoryStream(bytesArray, block.Bytes.Offset, block.Bytes.Count, false);
            }
        }
    }
}