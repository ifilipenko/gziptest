using System;
using Parallel.Compression.Compression;
using Parallel.Compression.Decompression;
using Parallel.Compression.Decompression.GzipSplitting;
using Parallel.Compression.Errors;
using Parallel.Compression.Func;
using Parallel.Compression.IO;

namespace Parallel.Compression
{
    public class FileCompression
    {
        private readonly IFileSystem fileSystem;
        private readonly IStreamCompressor compressor;
        private readonly IStreamDecompressor decompressor;

        public FileCompression(IFileSystem fileSystem, IStreamCompressor compressor, IStreamDecompressor decompressor)
        {
            this.fileSystem = fileSystem;
            this.compressor = compressor;
            this.decompressor = decompressor;
        }

        public Result<int, ErrorCodes?> CompressFiles(string inputFilePath, string outputFilePath)
        {
            if (inputFilePath == null)
                throw new ArgumentNullException(nameof(inputFilePath));
            if (outputFilePath == null)
                throw new ArgumentNullException(nameof(outputFilePath));
            
            var (inputStream, inputStreamError) = fileSystem.OpenFileToRead(inputFilePath);
            if (inputStreamError.HasValue)
                return inputStreamError;

            using (inputStream)
            {
                var (outputStream, outPutStreamError) = fileSystem.OpenFileToReadWrite(outputFilePath);
                if (outPutStreamError.HasValue)
                    return outPutStreamError;

                using (outputStream)
                {
                    return compressor.Compress(inputStream, outputStream);
                }
            }
        }
        
        public Result<int, ErrorCodes?> DecompressFile(string inputFilePath, string outputFilePath)
        {
            if (inputFilePath == null)
                throw new ArgumentNullException(nameof(inputFilePath));
            if (outputFilePath == null)
                throw new ArgumentNullException(nameof(outputFilePath));
            
            var (inputStream, inputStreamError) = fileSystem.OpenFileToRead(inputFilePath);
            if (inputStreamError.HasValue)
                return inputStreamError;

            using (inputStream)
            {
                var (outputStream, outPutStreamError) = fileSystem.OpenFileToReadWrite(outputFilePath);
                if (outPutStreamError.HasValue)
                    return outPutStreamError;

                using (outputStream)
                {
                    return decompressor.Decompress(inputStream, outputStream);
                }
            }
        }
    }
}