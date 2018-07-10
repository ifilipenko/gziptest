using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using FluentAssertions;
using FluentAssertions.Extensions;
using Parallel.Compression.Logging;
using Parallel.Compression.Tests.Helpers;
using Parallel.Compression.WorkersPool;
using Xunit;
using Xunit.Abstractions;

namespace Parallel.Compression.Tests
{
    public class WorkersPoolSpec
    {
        [SuppressMessage("ReSharper", "ObjectCreationAsStatement")]
        public class Ctor
        {
            [Theory]
            [InlineData(-1)]
            [InlineData(0)]
            public void Should_fail_when_given_invalid_workers(int workers)
            {
                Action action = () => new WorkersPool.WorkersPool(workers, new ConsoleLog());

                action.Should().Throw<ArgumentException>();
            }

            [Fact]
            public void Should_fail_when_given_null_log()
            {
                Action action = () => new WorkersPool.WorkersPool(1, null);

                action.Should().Throw<ArgumentNullException>();
            }
        }

        [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
        public class PushTask : IDisposable
        {
            private readonly WorkersPool.WorkersPool workersPool;
            private readonly ITestOutputHelper output;

            public PushTask(ITestOutputHelper output)
            {
                workersPool = new WorkersPool.WorkersPool(3, new TestLog(output));
                this.output = output;
            }

            public void Dispose()
            {
                output.WriteLine("\tDispose queue");
                workersPool.Dispose();
            }

            [Fact]
            public void Should_fail_when_given_null_task()
            {
                Action action = () => workersPool.PushTask(null);

                action.Should().Throw<ArgumentNullException>();
            }

            [Fact]
            public void Should_enqueue_given_task_and_run_into_free_worker()
            {
                var startSignal = new CountdownEvent(1);
                workersPool.PushTask(WorkerTasks.Task("task 1", startSignal: startSignal));

                startSignal.Wait();

                workersPool.TasksQueueSize.Should().Be(0);
            }

            [Fact]
            public void Should_enqueue_tasks_more_than_workers_count()
            {
                workersPool.PushTask(WorkerTasks.UnfinishedTask("task 1"));
                workersPool.PushTask(WorkerTasks.UnfinishedTask("task 2"));
                workersPool.PushTask(WorkerTasks.UnfinishedTask("task 3"));
                workersPool.PushTask(WorkerTasks.UnfinishedTask("task 4"));
                workersPool.PushTask(WorkerTasks.UnfinishedTask("task 5"));
                workersPool.PushTask(WorkerTasks.UnfinishedTask("task 6"));
                workersPool.PushTask(WorkerTasks.UnfinishedTask("task 7"));
                
                workersPool.TasksQueueSize.Should().BeInRange(4, 7);
            }

            [Theory]
            [InlineData(10, 3)]
            [InlineData(100, 3)]
            [InlineData(100, 10)]
            [InlineData(1000, 100)]
            public void Should_execute_all_tasks_after_many_iterations(int tasksCount, int workersCount)
            {
                using (var newWorkersPool = new WorkersPool.WorkersPool(workersCount, new TestLog(output)))
                {
                    var expectedResults = Enumerable.Range(0, tasksCount).ToList();
                    var actualResults = new int[tasksCount];
                    var startSignals = Enumerable.Range(0, tasksCount).Select(_ => new CountdownEvent(1)).ToArray();

                    var stopwatch = new Stopwatch();
                    using (new Disposables(startSignals))
                    using (var finishSignal = new CountdownEvent(tasksCount))
                    {
                        var workerTasks = Enumerable.Range(0, tasksCount)
                            .Select(
                                id => WorkerTasks.Task(
                                    "task " + id,
                                    startSignal: startSignals[id],
                                    finishSignal: finishSignal,
                                    action: () => actualResults[id] = id
                                )
                            )
                            .ToList();

                        stopwatch.Start();
                        for (var i = 0; i < workerTasks.Count; i++)
                        {
                            newWorkersPool.PushTask(workerTasks[i]);
                            startSignals[i].Wait();
                        }

                        finishSignal.Wait(5.Seconds());
                        stopwatch.Stop();
                    }

                    actualResults.Should().BeEquivalentTo(expectedResults);
                    output.WriteLine("Task finished for " + stopwatch.Elapsed);
                }
            }
        }

        [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
        public class TaskInProgress : IDisposable
        {
            private readonly ITestOutputHelper output;
            private readonly WorkersPool.WorkersPool workersPool;

            public TaskInProgress(ITestOutputHelper output)
            {
                this.output = output;
                workersPool = new WorkersPool.WorkersPool(3, new TestLog(output));
            }

            public void Dispose()
            {
                output.WriteLine("\tDispose queue");
                workersPool.Dispose();
            }

            [Fact]
            public void Should_return_0_in_progress_tasks_when_no_tasks_started()
            {
                workersPool.TaskInProgress.Should().Be(0);
            }

            [Fact]
            public void Should_set_in_progress_tasks_count_while_they_are_executing()
            {
                var startSignal = new CountdownEvent(3);

                workersPool.PushTask(WorkerTasks.UnfinishedTask("task 1", startSignal));
                workersPool.PushTask(WorkerTasks.UnfinishedTask("task 2", startSignal));
                workersPool.PushTask(WorkerTasks.UnfinishedTask("task 3", startSignal));

                startSignal.Wait();
                Thread.Sleep(100);

                var actual = workersPool.TaskInProgress;

                actual.Should().Be(3);
            }

            [Fact]
            public void Should_decrease_when_one_of_task_finished()
            {
                var task2FinishSignal = new CountdownEvent(1);
                var startSignal = new CountdownEvent(3);
                var task2BlockSignal = new SemaphoreSlim(0);

                workersPool.PushTask(WorkerTasks.UnfinishedTask("task 1", startSignal));
                workersPool.PushTask(WorkerTasks.Task("task 2", task2BlockSignal, startSignal, task2FinishSignal));
                workersPool.PushTask(WorkerTasks.UnfinishedTask("task 3", startSignal));

                startSignal.Wait();
                output.WriteLine("All task started");

                var beforeOneFinish = workersPool.TaskInProgress;
                task2BlockSignal.Release();

                task2FinishSignal.Wait();
                Thread.Sleep(100);
                output.WriteLine("Task 2 finished");

                beforeOneFinish.Should().Be(3);
                workersPool.TaskInProgress.Should().Be(2);
            }

            [Fact]
            public void Should_reset_in_progress_tasks_when_they_completed()
            {
                var finishSignal = new CountdownEvent(3);

                workersPool.PushTask(WorkerTasks.Task("task 1", finishSignal: finishSignal));
                workersPool.PushTask(WorkerTasks.Task("task 2", finishSignal: finishSignal));
                workersPool.PushTask(WorkerTasks.Task("task 3", finishSignal: finishSignal));

                finishSignal.Wait();
                Thread.Sleep(100);

                workersPool.TaskInProgress.Should().Be(0);
            }
        }

        private static class WorkerTasks
        {
            public static IWorkerTask UnfinishedTask(string id, CountdownEvent startSignal = null)
            {
                return Task(id, startSignal: startSignal, blockSignal: new SemaphoreSlim(0), disposeBlock: true);
            }

            public static IWorkerTask Task(string id, SemaphoreSlim blockSignal = null, CountdownEvent startSignal = null, CountdownEvent finishSignal = null, Action action = null, bool disposeBlock = false)
            {
                IDisposable resources = null;
                if (disposeBlock)
                {
                    resources = blockSignal;
                }

                return new DelegateWorkerTask(
                    id,
                    () =>
                    {
                        startSignal?.Signal();

                        blockSignal?.Wait();

                        action?.Invoke();

                        finishSignal?.Signal();
                    },
                    resources);
            }

            private class DelegateWorkerTask : IWorkerTask
            {
                private readonly Action action;
                private readonly IDisposable resources;

                public DelegateWorkerTask(string id, Action action, IDisposable resources)
                {
                    Id = id;
                    this.action = action;
                    this.resources = resources;
                }

                public string Id { get; }

                public void Execute()
                {
                    action();
                }

                public void Dispose()
                {
                    resources?.Dispose();
                }
            }
        }
    }
}