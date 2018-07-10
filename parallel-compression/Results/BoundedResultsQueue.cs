using System;
using System.Collections.Generic;
using System.Threading;
using Parallel.Compression.Logging;
using Parallel.Compression.Threading;

namespace Parallel.Compression.Results
{
    internal class BoundedResultsQueue<TResult> : IDisposable
    {
        private readonly TimeSpan resultRecheckTimeout;
        private readonly ILog log;
        private readonly BlockingQueue<ResultSlot> resultSlots;
        private readonly InterlockedBool isDisposed = new InterlockedBool(false);
        private readonly InterlockedBool isQueueingTurnOff = new InterlockedBool(false);
        private readonly Semaphore anyResultsWaiter;

        public BoundedResultsQueue(int capacity, TimeSpan resultRecheckTimeout, ILog log = null)
        {
            if (resultRecheckTimeout.Ticks < 0)
                throw new ArgumentException("Given negative timeout", nameof(resultRecheckTimeout));
            this.resultRecheckTimeout = resultRecheckTimeout;
            this.log = log;
            resultSlots = new BlockingQueue<ResultSlot>(capacity);
            anyResultsWaiter = new Semaphore(0, int.MaxValue);
        }

        public void TurnOffAcquiringSlots()
        {
            CheckDisposed();
            
            isQueueingTurnOff.Set(true);
            resultSlots.TurnOffQueueingNewElements();
        }

        public IResultSlot<TResult> AcquireFreeResultOrWait()
        {
            CheckDisposed();
            
            var resultSlot = new ResultSlot(anyResultsWaiter);
            CheckQueueingIsTurnOff();
            resultSlots.Enqueue(resultSlot);
            return resultSlot;
        }

        public IEnumerable<TResult> DequeResultsOnCompletion()
        {
            CheckDisposed();

            do
            {
                if (resultSlots.TryDequeIfHeadMatched(x => x.HasResult, out var slot))
                {
                    yield return slot.Result;
                }
                else
                {
                    anyResultsWaiter.WaitOne(resultRecheckTimeout);
                }
            } while (!isDisposed && (!isQueueingTurnOff || resultSlots.Size > 0));
        }

        public void Dispose()
        {
            if (!isQueueingTurnOff)
            {
                TurnOffAcquiringSlots();
            }
            isDisposed.Set(true);
            resultSlots?.Dispose();
            anyResultsWaiter.Dispose();
        }

        private void CheckQueueingIsTurnOff()
        {
            if (isQueueingTurnOff)
            {
                throw new InvalidOperationException("Queueing is off");
            }
        }

        private void CheckDisposed()
        {
            if (isQueueingTurnOff)
            {
                throw new InvalidOperationException("Queue is disposed");
            }
        }

        private class ResultSlot : IResultSlot<TResult>
        {
            private readonly Semaphore semaphore;

            public ResultSlot(Semaphore semaphore)
            {
                this.semaphore = semaphore;
            }

            public TResult Result { get; private set; }
            public bool HasResult { get; private set; }

            public void SetResult(TResult result)
            {
                HasResult = true;
                Result = result;
                semaphore.Release();
            }
        }
    }
}