using System;
using System.Threading;
using Parallel.Compression.Logging;

namespace Parallel.Compression.Threading
{
    internal class Limiter
    {
        private readonly object syncObject = new object();
        private int free;
        private readonly int maxValue;
        private readonly ILog log;
        private bool releasedForever;

        public Limiter(int free, int maxValue = int.MaxValue, ILog log = null)
        {
            if (free < 0)
                throw new ArgumentException("Counter can't be nagative number", nameof(free));
            if (maxValue < 1)
                throw new ArgumentException("Max counter can't be less than 1", nameof(free));
            if (free > maxValue)
                throw new ArgumentException("Counter can't be creater than max value", nameof(free));

            this.free = free;
            this.maxValue = maxValue;
            this.log = log ?? new StubLog();
        }

        public int CurrentFreeNumber => free;
        public int MaxLimit => maxValue;

        public bool TryRelease(out int result)
        {
            return TryRelease(1, out result);
        }
        
        public bool TryRelease(int count, out int result)
        {
            if (count <= 0)
                throw new ArgumentException("Releasing number can't be zero or negative number", nameof(count));
            
            var currentFree = free;
            if (releasedForever || currentFree == maxValue)
            {
                result = currentFree;
                return false;
            }

            lock (syncObject)
            {
                var oldFree = free;
                var newfree = Math.Min(maxValue, oldFree + count);
                var newCount = newfree - oldFree;
                if (count != newCount)
                {
                    result = oldFree;
                    return false;
                }
                while (count-- > 0)
                {
                    Monitor.Pulse(syncObject);
                }

                free = newfree;
                LogReleased(newfree, newfree - oldFree);
                result = oldFree;
                return true;
            }
        }

        public void ReleaseForever()
        {
            if (releasedForever)
            {
                return;
            }

            lock (syncObject)
            {
                free = maxValue;
                releasedForever = true;
                Monitor.PulseAll(syncObject);
                LogReleasedForever();
            }
        }

        public void WaitFree()
        {
            LogWaitStarts();

            Wait(out var counter);
            LogWaitDoneWithLock(counter);

            // do
            // {
            //     if (free == 0 && TryWait(out var counter))
            //     {
            //         LogWaitDoneWithLock(counter);
            //         return;
            //     }
            //
            //     if (TryDecreaseFree(out counter))
            //     {
            //         LogWaitDoneWithCas(counter);
            //         return;
            //     }
            // } while (true);

            void Wait(out int result)
            {
                if (releasedForever)
                {
                    result = free;
                    return;
                }

                lock (syncObject)
                {
                    if (releasedForever)
                    {
                        result = free;
                        return;
                    }

                    var currentFree = free;
                    if (currentFree == 0)
                    {
                        Monitor.Wait(syncObject);
                        currentFree = free;
                    }
                    
                    if (releasedForever)
                    {
                        result = free;
                        return;
                    }

                    if (currentFree > 0)
                    {
                        currentFree--;
                        free = currentFree;
                        result = currentFree;
                        return;
                    }

                    result = currentFree;
                }
            }

            // bool TryDecreaseFree(out int result)
            // {
            //     do
            //     {
            //         var oldFree = free;
            //         if (oldFree <= 0)
            //         {
            //             result = oldFree;
            //             return false;
            //         }
            //
            //         if (releasedForever)
            //         {
            //             result = free;
            //             return true;
            //         }
            //
            //         oldFree = free;
            //         var nextFree = Math.Max(0, oldFree - 1);
            //         if (Interlocked.CompareExchange(ref free, nextFree, oldFree) == oldFree)
            //         {
            //             result = nextFree;
            //             return true;
            //         }
            //     } while (true);
            // }
            //
            // void LogWaitDoneWithCas(int waitFinishWithCounter)
            // {
            //     if (log.IsDebugEnabled)
            //     {
            //         log.Debug($"Wait done with cas and counter {waitFinishWithCounter}");
            //     }
            // }

            void LogWaitDoneWithLock(int waitFinishWithCounter)
            {
                if (log.IsDebugEnabled)
                {
                    log.Debug($"Wait done with lock and counter {waitFinishWithCounter}");
                }
            }

            void LogWaitStarts()
            {
                if (log.IsDebugEnabled)
                {
                    log.Debug("Log wait starts");
                }
            }
        }

        private void LogReleased(int currentValue, int times)
        {
            if (log.IsDebugEnabled)
            {
                log.Debug($"Release {times} times and counter is {currentValue}");   
            }
        }

        private void LogReleasedForever()
        {
            if (log.IsDebugEnabled)
            {
                log.Debug($"Release forever with counter {free}");   
            }
        }
    }
}