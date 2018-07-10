using System;
using System.IO;
using System.IO.Compression;
using JetBrains.Annotations;
using Parallel.Compression.Decompression.Streams;

namespace Parallel.Compression.Decompression.GzipSplitting
{
    internal class StreamingGzipBlock : IGzipBlock
    {
        private readonly ExactlyPositionedAccoringToGzipBlockBoundsStream stream;

        public StreamingGzipBlock([NotNull] RewindableReadonlyStream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            this.stream = new ExactlyPositionedAccoringToGzipBlockBoundsStream(stream);
        }

        public void WriteDecompressedDataTo(Stream outputStream)
        {
            do
            {
                using (var gZipStream = new GZipStream(stream, CompressionMode.Decompress, true))
                {
                    gZipStream.CopyTo(outputStream);
                }
            } while (!stream.IsEndOfStream);
        }
    }
}