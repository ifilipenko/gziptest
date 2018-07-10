using System.IO;
using JetBrains.Annotations;
using Parallel.Compression.Errors;
using Parallel.Compression.Func;

namespace Parallel.Compression.Compression
{
    public interface IStreamCompressor
    {
        Result<int, ErrorCodes?> Compress([NotNull] Stream inputStream, [NotNull] Stream outputStream);
    }
}