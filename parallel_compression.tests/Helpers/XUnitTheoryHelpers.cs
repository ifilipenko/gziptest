using System.Collections.Generic;
using Xunit;

namespace Parallel.Compression.Tests.Helpers
{
    public static class XUnitTheoryHelpers
    {
        public static TheoryData<T> ToTheoryData<T>(this IEnumerable<T> enumerable)
        {
            var theoryData = new TheoryData<T>();
            foreach (var item in enumerable)
            {
                theoryData.Add(item);
            }

            return theoryData;
        }
    }
}