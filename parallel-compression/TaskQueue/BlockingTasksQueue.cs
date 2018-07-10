using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Parallel.Compression.Logging;
using Parallel.Compression.Results;
using Parallel.Compression.Threading;
using Parallel.Compression.WorkersPool;

namespace Parallel.Compression.TaskQueue
{
    internal class BlockingTasksQueue : IDisposable
    {
        private readonly IWorkersPool workers;
        private readonly InterlockedBool isDisposed = new InterlockedBool(false);
        private readonly InterlockedBool isEnds = new InterlockedBool(false);
        private readonly BoundedResultsQueue<TaskResult> results;

        public BlockingTasksQueue(int capacity, [NotNull] IWorkersPool workers, [NotNull] ILog log)
        {
            if (capacity <= 0)
            {
                throw new ArgumentException("Capacity can't be negative or zero number", nameof(capacity));
            }

            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            this.workers = workers ?? throw new ArgumentNullException(nameof(workers));
            results = new BoundedResultsQueue<TaskResult>(capacity, TimeSpan.FromMilliseconds(100), log.WithPrefix("[BoundedResultsQueue]"));
        }

        public void EnqueueTask([NotNull] ITask task)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));

            CheckDisposed();

            var resultSlot = results.AcquireFreeResultOrWait();
            workers.PushTask(new TaskWithResult(resultSlot, task));
        }

        public IEnumerable<TaskResult> ConsumeTaskResults()
        {
            CheckDisposed();

            return results.DequeResultsOnCompletion();
        }

        public void EndTasks()
        {
            CheckDisposed();

            results.TurnOffAcquiringSlots();
            isEnds.Set(true);
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            isDisposed.Set(true);
            isEnds.Set(true);

            workers.Dispose();
            results.Dispose();
        }

        private void CheckDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(BlockingTasksQueue));
            }
        }

        private class TaskWithResult : IWorkerTask
        {
            private readonly IResultSlot<TaskResult> resultSlot;
            private readonly ITask task;

            public TaskWithResult(IResultSlot<TaskResult> resultSlot, ITask task)
            {
                this.resultSlot = resultSlot;
                this.task = task;
            }

            public void Dispose()
            {
                task?.Dispose();
            }

            public void Execute()
            {
                object result = null;
                Exception exception = null;
                try
                {
                    result = task.Execute();
                }
                catch (Exception ex)
                {
                    exception = ex;
                }

                resultSlot.SetResult(new TaskResult(result, exception));
            }

            public string Id => task.Id;
        }
    }
}