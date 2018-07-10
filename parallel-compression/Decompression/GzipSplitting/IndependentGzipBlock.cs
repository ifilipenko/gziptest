using System;
using System.IO;
using System.IO.Compression;
using JetBrains.Annotations;

namespace Parallel.Compression.Decompression.GzipSplitting
{
    public class IndependentGzipBlock : IGzipBlock
    {
        private readonly Stream blockStream;

        public IndependentGzipBlock([NotNull] Stream blockStream)
        {
            this.blockStream = blockStream ?? throw new ArgumentNullException(nameof(blockStream));
        }

        public byte[] Decompress()
        {
            using (var outputStream = new MemoryStream())
            using (var gZipStream = new GZipStream(blockStream, CompressionMode.Decompress, true))
            {
                gZipStream.CopyTo(outputStream);

                blockStream.Position = 0;
                return outputStream.ToArray();
            }
        }
    }
}