using System;

namespace Parallel.Compression.WorkersPool
{
    internal class DisposingOptions
    {
        public static readonly DisposingOptions Default = new DisposingOptions();

        public bool InterruptThreadInAnyCase { get; set; } = true;
        public TimeSpan? WaitThreadTimeout { get; set; } = null;// TimeSpan.FromMilliseconds(500);
    }
}