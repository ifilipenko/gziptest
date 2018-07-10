using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Extensions;
using NSubstitute;
using Parallel.Compression.Logging;
using Parallel.Compression.TaskQueue;
using Parallel.Compression.Tests.Helpers;
using Parallel.Compression.WorkersPool;
using Xunit;
using Xunit.Abstractions;
using DelegateTask = Parallel.Compression.TaskQueue.DelegateTask;

namespace Parallel.Compression.Tests
{
    public class BlockingTasksQueueSpec
    {
        [SuppressMessage("ReSharper", "ObjectCreationAsStatement")]
        [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
        public class Ctor
        {
            [Fact]
            public void Should_fail_when_given_null_worker_pool()
            {
                Action action = () => new BlockingTasksQueue(1, null, new ConsoleLog());

                action.Should().Throw<ArgumentNullException>();
            }
            
            [Theory]
            [InlineData(-1)]
            [InlineData(0)]
            public void Should_fail_when_given_invalid_capacity(int capacity)
            {
                Action action = () => new BlockingTasksQueue(capacity, Substitute.For<IWorkersPool>(), new ConsoleLog());

                action.Should().Throw<ArgumentException>();
            }

            [Fact]
            public void Should_fail_when_given_null_log()
            {
                Action action = () => new BlockingTasksQueue(1, Substitute.For<IWorkersPool>(), null);

                action.Should().Throw<ArgumentNullException>();
            }
        }

        public class Blocking_Task_Producing_Consuming
        {
            private readonly ITestOutputHelper output;
            private readonly Random random = new Random();
            private TestLog log;

            public Blocking_Task_Producing_Consuming(ITestOutputHelper output)
            {
                this.output = output;
                log = new TestLog(output);
            }

            [SuppressMessage("ReSharper", "AccessToDisposedClosure")]
            [Theory]
            [InlineData(10, 3)]
            [InlineData(100, 3)]
            [InlineData(100, 10)]
            public void Should_consume_all_produced_tasks_after_many_iterations(int tasksCount, int paralelizmLevel)
            {
                using (var taskQueue = new BlockingTasksQueue(paralelizmLevel, new WorkersPool.WorkersPool(paralelizmLevel, log), log))
                {
                    var expectedResults = Enumerable.Range(1, tasksCount).Cast<object>().ToArray();

                    var producing = Task.Run(
                        () =>
                        {
                            foreach (var task in CreateTasks(0, tasksCount))
                            {
                                output.WriteLine("Adding " + task.Id);
                                taskQueue.EnqueueTask(task);
                                output.WriteLine("Added " + task.Id);
                            }

                            taskQueue.EndTasks();
                            output.WriteLine("producer stopped");
                        });

                    var consuming = Task.Run(
                        () =>
                        {
                            var results = new List<object>();

                            foreach (var result in taskQueue.ConsumeTaskResults())
                            {
                                if (result.IsFailed)
                                    throw new InvalidOperationException("Unexpected error");
                                output.WriteLine("Consumed " + result.Result);
                                results.Add(result.Result);
                            }
                            
                            output.WriteLine("consumer stopped");
                            return results;
                        });

                    var allConsumedTask = Task.WhenAll(producing, consuming);
                    allConsumedTask.Wait(5.Seconds());

                    allConsumedTask.IsCompletedSuccessfully.Should().BeTrue();

                    var allResults = consuming.Result;
                    PrintResults(allResults);
                    allResults.Should().BeEquivalentTo(expectedResults);
                }
            }

            [SuppressMessage("ReSharper", "AccessToDisposedClosure")]
            [Theory]
            [InlineData(10, 3)]
            [InlineData(100, 3)]
            [InlineData(100, 10)]
            [SuppressMessage("ReSharper", "AccessToModifiedClosure")]
            public void Should_consume_many_tasks_by_many_producers_and_many_consumers(int tasksPerProducerCount, int paralelizmLevel)
            {
                using (var taskQueue = new BlockingTasksQueue(paralelizmLevel, new WorkersPool.WorkersPool(paralelizmLevel, log), new TestLog(output)))
                {
                    var expectedResults = Enumerable.Range(1, 3*tasksPerProducerCount).Cast<object>().ToArray();

                    var producers = new[]
                    {
                        Task.Run(() => CreateTasks(0, tasksPerProducerCount).ForEach(Enqueue)),
                        Task.Run(() => CreateTasks(tasksPerProducerCount, tasksPerProducerCount).ForEach(Enqueue)),
                        Task.Run(() => CreateTasks(tasksPerProducerCount*2, tasksPerProducerCount).ForEach(Enqueue))
                    };

                    var consumers = new[]
                    {
                        Task.Run(() => Consume(1)),
                        Task.Run(() => Consume(2)),
                        Task.Run(() => Consume(3))
                    };

                    var producersEnds = Task.WhenAll(producers);
                    producersEnds.Wait();
                    taskQueue.EndTasks();

                    var consumersEnds = Task.WhenAll(consumers)
                        .ContinueWith(t => t.Result.SelectMany(x => x).ToList());
                    var allResults = consumersEnds.Result;

                    producersEnds.IsCompletedSuccessfully.Should().BeTrue();
                    consumersEnds.IsCompletedSuccessfully.Should().BeTrue();
                    allResults.Should().HaveSameCount(expectedResults).And.Contain(expectedResults);

                    void Enqueue(ITask task)
                    {
                        output.WriteLine("Adding " + task.Id);
                        taskQueue.EnqueueTask(task);
                        output.WriteLine("Added " + task.Id);
                    }

                    List<object> Consume(int consumerId)
                    {
                        var results = new List<object>();
                        foreach (var taskResult in taskQueue.ConsumeTaskResults())
                        {
                            if (taskResult.IsFailed)
                                throw new InvalidOperationException("Unexpected error");
                            output.WriteLine($"Consumer {consumerId} consumed {taskResult.Result}");
                            results.Add(taskResult.Result);
                        }

                        output.WriteLine($"Consumer {consumerId} results: {results.Count} => {string.Join(",", results)}");
                        return results;
                    }
                }
            }

            private IEnumerable<DelegateTask> CreateTasks(int startIndex, int tasksCount)
            {
                return Enumerable.Range(startIndex, tasksCount)
                    .Select(
                        x =>
                        {
                            var spinIterations = random.Next(1, 1000);
                            return new DelegateTask(
                                "task " + x,
                                () =>
                                {
                                    Thread.SpinWait(spinIterations);
                                    var result = x + 1;
                                    output.WriteLine($"Thr {Thread.CurrentThread.ManagedThreadId}. Task 'task {x}' complete with result {result} for {spinIterations} interations");
                                    return result;
                                });
                        })
                    .ToList();
            }

            private void PrintResults<T>(IList<T> results)
            {
                output.WriteLine($"Results: {results.Count} => {string.Join(",", results)}");
            }
        }
    }
}