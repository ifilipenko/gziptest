using System;
using System.Linq;

namespace Parallel.Compression.Tests.Helpers
{
    internal static class StringHelpers
    {
        private static readonly Random Random = new Random();

        public static string Shuffle(this string value)
        {
            var chars = value.OrderBy(c => Random.Next()).ToArray();
            return new string(chars);
        }
    }
}