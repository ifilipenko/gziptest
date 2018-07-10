using System;
using System.Collections.Generic;

namespace Parallel.Compression.Helpers
{
    internal static class Empty<T>
    {
        public static T[] Array { get; } = new T[0];
        public static ArraySegment<T> ArraySegment { get; } = new ArraySegment<T>(Array);
        public static IReadOnlyList<T> List { get; } = new List<T>(0).AsReadOnly();
    }
}