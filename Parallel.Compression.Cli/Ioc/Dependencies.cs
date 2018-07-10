using Parallel.Compression.Compression;
using Parallel.Compression.Configuration;
using Parallel.Compression.Decompression;
using Parallel.Compression.IO;
using Parallel.Compression.Logging;

namespace Parallel.Compression.Cli.Ioc
{
    internal class Dependencies
    {
        private ILog log;

        public Dependencies(ILog log)
        {
            this.log = log;
        }

        public FileCompression GetFileCompression(CompressorSettings settings)
        {
            var fileSystem = new FileSystem();
            var streamCompressor = new StreamCompressor(new GzipBlockCompression(), settings, log);
            var streamDecompressor = new StreamDecompressor(settings, log);
            
            return new FileCompression(fileSystem, streamCompressor, streamDecompressor);
        }
    }
}