using System;
using System.Collections.Generic;
using System.Threading;
using JetBrains.Annotations;
using Parallel.Compression.Func;
using Parallel.Compression.Logging;

namespace Parallel.Compression.Threading
{
    internal class BlockingQueue<T> : IDisposable
    {
        public enum DequeueStatus
        {
            Success = 0,
            QueueingCancelledButQueueIsEmpty = 1
        }
        
        private readonly int capacity;
        private readonly ILog log;
        private readonly object syncObject = new object();
        private readonly Queue<T> queue = new Queue<T>();
        private readonly Limiter dequeWaiter = new Limiter(0);
        private readonly Limiter enqueueLimiter;
        private readonly InterlockedBool isQueueingTurnOff = new InterlockedBool(false);
        private readonly InterlockedBool isDisposed = new InterlockedBool(false);
        private int size;

        public BlockingQueue(int capacity, ILog log = null)
        {
            if (capacity < 1)
                throw new ArgumentException("Capacity can't be negative or zero", nameof(capacity));
            this.capacity = capacity;
            this.log = log ?? new StubLog();
            enqueueLimiter = new Limiter(capacity);
        }

        public int Size => size;
        public bool IsFull => size >= capacity;
        public int Capacity => capacity;
        public bool IsQueueingTurnOff => isQueueingTurnOff;

        public void TurnOffQueueingNewElements()
        {
            CheckDisposed();
            
            if (!isQueueingTurnOff)
            {
                isQueueingTurnOff.Set(true);
                dequeWaiter.ReleaseForever();
            }
        }

        public void Enqueue(T value)
        {
            CheckDisposed();
            CheckQueueingIsTurnOff();
            EnqueueWait();

            lock (syncObject)
            {
                queue.Enqueue(value);
                Interlocked.Increment(ref size);
            }

            DequeueRelease();
        }

        public T Dequeue()
        {
            CheckDisposed();
            DequeueWait();

            CheckQueueingIsTurnOff();
            T result;
            lock (syncObject)
            {
                CheckQueueingIsTurnOff();
                result = queue.Dequeue();
                Interlocked.Decrement(ref size);
            }

            EnqueueRelease();
            return result;
        }

        public Result<T, DequeueStatus> TryDequeue()
        {
            CheckDisposed();
            DequeueWait();

            T result;
            lock (syncObject)
            {
                if (queue.Count == 0)
                {
                    return DequeueStatus.QueueingCancelledButQueueIsEmpty;
                }

                result = queue.Dequeue();
                Interlocked.Decrement(ref size);
            }

            EnqueueRelease();
            return result;
        }
        
        public bool TryDequeIfHeadMatched([NotNull] Func<T, bool> condition, out T element)
        {
            CheckDisposed();
            
            if (condition == null)
                throw new ArgumentNullException(nameof(condition));
            DequeueWait();

            bool dequeue;
            lock (syncObject)
            {
                if (queue.Count == 0)
                {
                    element = default(T);
                    dequeue = false;
                }
                else
                {
                    element = queue.Peek();
                    dequeue = condition(element);
                }

                if (dequeue)
                {
                    element = queue.Dequeue();
                    Interlocked.Decrement(ref size);
                }
            }

            if (dequeue)
            {
                EnqueueRelease();
            }
            else
            {
                DequeueRelease();
            }
            
            return dequeue;
        }

        public void Dispose()
        {
            isDisposed.Set(true);
            isQueueingTurnOff.Set(true);
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
            if (isDisposed)
            {
                throw new InvalidOperationException("Queue is disposed");
            }
        }

        private void DequeueWait()
        {
            if (!isQueueingTurnOff)
            {
                if (log.IsDebugEnabled)
                {
                    log.Debug($"Dequeue wait for {dequeWaiter.CurrentFreeNumber} times");
                }
                
                dequeWaiter.WaitFree();
                
                if (log.IsDebugEnabled)
                {
                    log.Debug($"Dequeue allowed and current limit is {dequeWaiter.CurrentFreeNumber}");
                }
            }
        }

        private void DequeueRelease()
        {
            if (!isQueueingTurnOff)
            {
                if (dequeWaiter.TryRelease(out var dequeueLimit) && log.IsDebugEnabled)
                {
                    log.Debug($"Dequeue released and current limit is {dequeueLimit}");
                }
            }
        }
        
        private void EnqueueRelease()
        {
            if (enqueueLimiter.TryRelease(out var enqueueLimit) && log.IsDebugEnabled)
            {
                log.Debug($"Enqueue released and current limit is {enqueueLimit}");
            }
        }

        private void EnqueueWait()
        {
            if (log.IsDebugEnabled)
            {
                log.Debug($"Enqueue wait for {enqueueLimiter.CurrentFreeNumber} times");
            }
            
            enqueueLimiter.WaitFree();
            
            if (log.IsDebugEnabled)
            {
                log.Debug($"Enqueue allowed and current limit is {enqueueLimiter.CurrentFreeNumber}");
            }
        }
    }
}