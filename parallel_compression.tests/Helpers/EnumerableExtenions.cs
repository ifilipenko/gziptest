using System;
using System.Collections.Generic;
using System.Linq;

namespace Parallel.Compression.Tests.Helpers
{
    internal static class EnumerableExtenions
    {
        public static void Enumerate<T>(this IEnumerable<T> enumerable)
        {
            // ReSharper disable once UnusedVariable
            var array = enumerable.ToArray();
        }
        
        public static void ForEach<T>(this IEnumerable<T> enumerable, Action<T> action)
        {
            foreach (var item in enumerable)
            {
                action(item);
            }
        }
    }
}