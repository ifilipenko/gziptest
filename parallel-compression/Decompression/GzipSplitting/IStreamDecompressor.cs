using System.IO;
using JetBrains.Annotations;
using Parallel.Compression.Errors;
using Parallel.Compression.Func;

namespace Parallel.Compression.Decompression.GzipSplitting
{
    public interface IStreamDecompressor
    {
        Result<int, ErrorCodes?> Decompress([NotNull] Stream inputStream, [NotNull] Stream outputStream);
    }
}