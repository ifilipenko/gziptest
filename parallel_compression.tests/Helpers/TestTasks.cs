using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Parallel.Compression.Logging;
using Parallel.Compression.TaskQueue;
using Parallel.Compression.Threading;

namespace Parallel.Compression.Tests.Helpers
{
    internal class TestTasks : IDisposable
    {
        private readonly List<Task> createdTasks = new List<Task>();
        private readonly InterlockedBool isDisposed = new InterlockedBool(false);
        private readonly ILog log;
        private int completed;
        private int called;

        public TestTasks(ILog log)
        {
            this.log = log;
        }

        public int Called => called;

        public ITask[] Create(params string[] titles)
        {
            return titles.Select(x => NewTask(x, null)).ToArray();
        }

        public ITask[] Create(params (string name, object result)[] titles)
        {
            return titles.Select(x => NewTask(x.name, x.result)).ToArray();
        }

        public void ContinueTasksAndWaitEnd(IEnumerable<ITask> tasks)
        {
            var taskDecorators = tasks.ToArray();
            ContinueTasks(taskDecorators);
            WaitEnd(taskDecorators);
        }

        public void ContinueTasks(IEnumerable<ITask> tasks)
        {
            log.Debug("Continue tasks");

            var count = 0;
            foreach (var taskDecorator in tasks.Cast<TaskDecorator>())
            {
                taskDecorator.Unwrap().Continue();
                count++;
            }

            log.Debug($"Continue tasks {count}");
        }

        public void WaitEnd(IEnumerable<ITask> tasks)
        {
            log.Debug("Wait finish for tasks");

            var count = 0;
            foreach (var taskDecorator in tasks.Cast<TaskDecorator>())
            {
                taskDecorator.Unwrap().WaitEnd();
                count++;
            }

            Thread.Sleep(200);
            log.Debug($"Waited tasks {count}");
        }

        public void WaitStart(IEnumerable<ITask> tasks)
        {
            log.Debug("Wait start for tasks");

            var count = 0;
            foreach (var taskDecorator in tasks.Cast<TaskDecorator>())
            {
                taskDecorator.Unwrap().WaitStart();
                count++;
            }

            log.Debug($"Waited started tasks {count}");
        }

        public void SetException(ITask task, Exception exception)
        {
            var taskDecorator = (TaskDecorator)task;
            taskDecorator.Unwrap().SetException(exception);
        }

        public void Dispose()
        {
            isDisposed.Set(true);
            foreach (var task in createdTasks)
            {
                task.Dispose();
            }

            createdTasks.Clear();
        }

        private ITask NewTask(string title, object result)
        {
            var task = new Task(this, title, result, log);
            createdTasks.Add(task);
            return new TaskDecorator(task);
        }

        private class TaskDecorator : ITask
        {
            private readonly Task task;

            public TaskDecorator(Task task)
            {
                this.task = task;
            }

            public string Id => task.Name;

            public Task Unwrap()
            {
                return task;
            }

            public object Execute()
            {
                return task.Execute();
            }

            public void Dispose()
            {
                task?.Dispose();
            }
        }

        private class Task : IDisposable
        {
            private readonly TestTasks testTasks;
            private readonly ManualResetEventSlim endWaiter = new ManualResetEventSlim(false);
            private readonly ManualResetEventSlim startWaiter = new ManualResetEventSlim(false);
            private readonly InterlockedBool isStarted = new InterlockedBool(false);
            private readonly InterlockedBool isFinished = new InterlockedBool(false);
            private readonly InterlockedBool isPaused = new InterlockedBool(true);
            private readonly object result;
            private readonly ILog log;
            private ManualResetEventSlim continueExecutionSignal = new ManualResetEventSlim(false);
            private Exception exception;

            public Task(TestTasks testTasks, string name, object result, ILog log)
            {
                this.testTasks = testTasks;
                Name = name;
                this.result = result;
                this.log = log;
            }

            public string Name { get; }

            private bool InProgress => isStarted && !isFinished;

            public void WaitEnd()
            {
                if (!isFinished)
                {
                    log.Debug($"End waiting for task {ToString()}");
                    endWaiter.Wait(TimeSpan.FromMilliseconds(1500));
                }
            }

            public void WaitStart()
            {
                if (!isStarted)
                {
                    log.Debug($"Start waiting for task {ToString()}");
                    startWaiter.Wait(TimeSpan.FromMilliseconds(1500));
                }
            }

            public void SetException(Exception exceptionAfterContinue)
            {
                this.exception = exceptionAfterContinue;
            }

            public void Continue()
            {
                if (!isPaused)
                    return;

                continueExecutionSignal.Set();
                isPaused.Set(false);
                log.Debug($"Execution was resumed for task: {this}");
            }

            public object Execute()
            {
                isStarted.Set(true);
                startWaiter.Set();

                var startOrder = Interlocked.Increment(ref testTasks.called);
                log.Debug($"Staring task '{Name}' in order {startOrder}");

                continueExecutionSignal.Wait();
                isPaused.Set(false);

                if (exception != null)
                {
                    throw exception;
                }

                var completeOrder = Interlocked.Increment(ref testTasks.completed);
                log.Debug($"Complete task '{Name}' in order {completeOrder}");

                endWaiter.Set();
                isFinished.Set(true);

                return result;
            }

            public void Dispose()
            {
                isPaused.Set(false);
                if (isStarted)
                {
                    continueExecutionSignal.Set();
                    endWaiter.Set();
                    startWaiter.Set();
                }

                continueExecutionSignal.Dispose();
                continueExecutionSignal = null;
                endWaiter.Dispose();
                startWaiter.Dispose();
            }

            public override string ToString()
            {
                var states = new List<string>(4);
                if (isStarted)
                {
                    states.Add("started");
                }

                if (InProgress)
                {
                    states.Add("in progress");
                }

                if (isPaused)
                {
                    states.Add("paused");
                }

                if (isFinished)
                {
                    states.Add("finished");
                }

                var stateText = string.Join(", ", states);
                return $"'{Name}' ({stateText})";
            }
        }
    }
}