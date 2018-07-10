using System.IO;
using System.Text;

namespace Parallel.Compression.Tests.Helpers
{
    internal static class TestData
    {
        public static string ShortFileContent => "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.";
        public static string InputFileContent => "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.";

        public static MemoryStream GenerateStream(string content)
        {
            var stream = new MemoryStream();
            using (var streamWriter = new StreamWriter(stream, Encoding.ASCII, 4*1024, true))
            {
                streamWriter.Write(content);
            }

            stream.Position = 0;
            return stream;
        }
    }
}