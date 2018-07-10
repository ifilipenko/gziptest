using System.IO;

namespace Parallel.Compression.BenchmarkTests.Files
{
    internal static class FileUtils
    {
        public static void DeleteIfExists(string filePath)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}