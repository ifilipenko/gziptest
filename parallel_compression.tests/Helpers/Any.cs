using System.IO;
using NSubstitute;

namespace Parallel.Compression.Tests.Helpers
{
    internal static class Any
    {
        public static byte[] Buffer => Arg.Any<byte[]>();
        public static int Offset => Arg.Any<int>();
        public static int Count => Arg.Any<int>();
        public static long Long => Arg.Any<long>();
        public static SeekOrigin Origin => Arg.Any<SeekOrigin>();
    }
}