using System.IO;
using JetBrains.Annotations;
using Parallel.Compression.Errors;
using Parallel.Compression.Func;

namespace Parallel.Compression.IO
{
    public interface IFileSystem
    {
        Result<Stream, ErrorCodes?> OpenFileToRead([NotNull] string inputFilePath);
        Result<Stream, ErrorCodes?> OpenFileToReadWrite([NotNull] string inputFilePath);
    }
}
