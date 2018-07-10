using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using JetBrains.Annotations;
using Parallel.Compression.Logging;
using Parallel.Compression.TaskQueue;
using Parallel.Compression.Threading;

namespace Parallel.Compression.WorkersPool
{
    internal class WorkersPool : IWorkersPool
    {
        private static readonly IWorkerTask EndOfTaskMarker = null;
        private readonly ILog log;
        private readonly WorkerProgress progress;
        private readonly object syncObject = new object();
        private readonly List<Worker> workers;
        private readonly Queue<IWorkerTask> taskQueue;
        private readonly InterlockedBool isDisposed = new InterlockedBool(false);

        public WorkersPool(int workersCount, ILog log, DisposingOptions disposingOptions = null)
        {
            if (workersCount <= 0)
            {
                throw new ArgumentException("Workers count can't be negative or zero number", nameof(workersCount));
            }

            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            disposingOptions = disposingOptions ?? DisposingOptions.Default;

            this.log = log.WithPrefix($"[{nameof(WorkersPool)}]");
            taskQueue = new Queue<IWorkerTask>(workersCount);
            progress = new WorkerProgress();

            workers = new List<Worker>(workersCount);
            for (var i = 0; i < workersCount; i++)
            {
                workers.Add(new Worker(taskQueue, ref syncObject, progress, log, disposingOptions));
            }
        }

        public int TasksQueueSize
        {
            get
            {
                CheckDisposed();
                return progress.TaskQueueSize;
            }
        }

        public int TaskInProgress
        {
            get
            {
                CheckDisposed();
                return progress.TaskCountInProgress;
            }
        }

        public void PushTask([NotNull] IWorkerTask task)
        {
            CheckDisposed();

            if (task == null)
                throw new ArgumentNullException(nameof(task));

            lock (syncObject)
            {
                taskQueue.Enqueue(task);
                progress.TaskQueueSize = taskQueue.Count;
                LogTaskWasEnqueue(task);
                Monitor.PulseAll(syncObject);
            }
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            lock (syncObject)
            {
                taskQueue.Enqueue(EndOfTaskMarker);
                Monitor.PulseAll(syncObject);
            }

            isDisposed.Set(true);
            foreach (var worker in workers)
            {
                worker.Dispose();
            }

            lock (syncObject)
            {
                while (taskQueue.Count > 0)
                {
                    var task = taskQueue.Dequeue();
                    task?.Dispose();
                }
            }

            workers.Clear();
        }

        private void CheckDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(BlockingTasksQueue));
            }
        }

        private void LogTaskWasEnqueue(IWorkerTask task)
        {
            if (log.IsDebugEnabled)
            {
                log.Debug($"Task '{task.Id}' was enqueue. {progress.DumpTasksStat()}");
            }
        }

        private class WorkerProgress
        {
            private int taskInProgress;

            public int TaskCountInProgress => taskInProgress;
            public int TaskQueueSize { get; set; }

            public void StartNewTask()
            {
                Interlocked.Increment(ref taskInProgress);
            }

            public void EndTask()
            {
                Interlocked.Decrement(ref taskInProgress);
            }

            public string DumpTasksStat()
            {
                return $"Tasks queue size {TaskQueueSize}, tasks in progress {taskInProgress}";
            }
        }

        private class Worker : IDisposable
        {
            private readonly Queue<IWorkerTask> taskQueue;
            private readonly object syncRoot;
            private readonly WorkerProgress progress;
            private readonly DisposingOptions disposingOptions;
            private readonly ILog log;
            private readonly InterlockedBool isCanceled = new InterlockedBool(false);
            private Thread thread;

            public Worker(
                Queue<IWorkerTask> taskQueue,
                ref object syncRoot,
                WorkerProgress progress,
                ILog log,
                DisposingOptions disposingOptions)
            {
                this.taskQueue = taskQueue;
                this.syncRoot = syncRoot;
                this.progress = progress;
                this.disposingOptions = disposingOptions;

                thread = new Thread(WorkerRoutine) {IsBackground = true};
                this.log = log.WithPrefix($"[Thread {thread.ManagedThreadId}]");
                thread.Start();
            }

            ~Worker()
            {
                thread?.Abort();
            }

            public void Dispose()
            {
                isCanceled.Set(true);
                if (thread == null)
                    return;

                if (disposingOptions.InterruptThreadInAnyCase)
                {
                    thread.Interrupt();
                }
                else if (thread.ThreadState == System.Threading.ThreadState.WaitSleepJoin)
                {
                    thread.Interrupt();
                }

                if (disposingOptions.WaitThreadTimeout.HasValue)
                {
                    thread.Join(disposingOptions.WaitThreadTimeout.Value);
                }
                else
                {
                    thread.Join();
                }

                thread = null;
            }

            private void WorkerRoutine()
            {
                while (!isCanceled)
                {
                    try
                    {
                        using (var task = WaitNewTask())
                        {
                            if (!isCanceled && !ReferenceEquals(task, EndOfTaskMarker))
                            {
                                ExecuteTask(task);
                            }
                        }
                    }
                    catch (ThreadInterruptedException)
                    {
                        return;
                    }
                }
            }

            private IWorkerTask WaitNewTask()
            {
                lock (syncRoot)
                {
                    while (taskQueue.Count == 0)
                    {
                        if (isCanceled)
                            return EndOfTaskMarker;

                        Monitor.Wait(syncRoot);
                    }

                    if (isCanceled)
                        return EndOfTaskMarker;

                    var task = taskQueue.Dequeue();
                    progress.TaskQueueSize = taskQueue.Count;

                    return task;
                }
            }

            private void ExecuteTask(IWorkerTask task)
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();

                progress.StartNewTask();
                LogTaskStarted(task);

                try
                {
                    if (isCanceled)
                        return;

                    task.Execute();

                    progress.EndTask();
                }
                finally
                {
                    stopwatch.Stop();
                    LogTaskFinished(task, stopwatch.Elapsed);
                }
            }

            private void LogTaskFinished(IWorkerTask task, TimeSpan elapsed)
            {
                if (log.IsDebugEnabled && !ReferenceEquals(task, EndOfTaskMarker))
                {
                    log.Debug($"Task '{task.Id}' executed at {elapsed}. {progress.DumpTasksStat()}");
                }
            }

            private void LogTaskStarted(IWorkerTask task)
            {
                if (log.IsDebugEnabled && !ReferenceEquals(task, EndOfTaskMarker))
                {
                    log.Debug($"Task '{task.Id}' was chosen to run. {progress.DumpTasksStat()}");
                }
            }
        }
    }
}