using System;
using System.Diagnostics;
using System.Threading;

namespace Parallel.Compression.Results
{
    internal static class TimeoutWaiter
    {
        private static readonly TimeSpan Precision = TimeSpan.FromMilliseconds(5);
        private static readonly TimeSpan FastWaitThreshold = TimeSpan.FromMilliseconds(500);

        public static void Wait(TimeSpan timeout)
        {
            if (timeout > FastWaitThreshold)
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();

                var remaining = timeout - stopwatch.Elapsed;
                while (remaining > Precision)
                {
                    Thread.SpinWait(500);
                    remaining = timeout - stopwatch.Elapsed;
                }
            }
            else
            {
                Thread.Sleep(timeout);
            }
        }
    }
}