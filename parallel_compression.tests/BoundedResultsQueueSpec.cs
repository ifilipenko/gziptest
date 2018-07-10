using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Extensions;
using Parallel.Compression.Logging;
using Parallel.Compression.Results;
using Parallel.Compression.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Parallel.Compression.Tests
{
    public class BoundedResultsQueueSpec
    {
        [SuppressMessage("ReSharper", "ObjectCreationAsStatement")]
        public class Ctor
        {
            [Theory]
            [InlineData(-1)]
            [InlineData(0)]
            public void Should_fail_when_given_invalid_capacity(int capacity)
            {
                Action action = () => new BoundedResultsQueue<int>(capacity, 1.Seconds(), new StubLog());

                action.Should().Throw<ArgumentException>();
            }

            [Fact]
            public void Should_fail_when_given_negative_timeout()
            {
                var timeout = (-1).Seconds();

                Action action = () => new BoundedResultsQueue<int>(1, timeout, new StubLog());

                action.Should().Throw<ArgumentException>();
            }
        }

        public class AcquireFreeResultOrWait : IDisposable
        {
            private readonly BoundedResultsQueue<int> boundedResultsQueue;

            public AcquireFreeResultOrWait(ITestOutputHelper output)
            {
                boundedResultsQueue = new BoundedResultsQueue<int>(3, 1.Seconds(), new TestLog(output));
            }

            [Fact]
            public void Should_return_free_slot_when_queue_is_empty()
            {
                var slot = boundedResultsQueue.AcquireFreeResultOrWait();

                slot.Should().NotBeNull();
            }

            [Fact]
            public void Should_block_thread_when_all_slots_are_acq()
            {
                var allTaskCompleted = Task.WhenAll(
                    Task.Run(() => boundedResultsQueue.AcquireFreeResultOrWait()),
                    Task.Run(() => boundedResultsQueue.AcquireFreeResultOrWait()),
                    Task.Run(() => boundedResultsQueue.AcquireFreeResultOrWait())
                );
                allTaskCompleted.Wait(200);

                allTaskCompleted.IsCompleted.Should().BeTrue();
            }

            [Fact]
            public void Should_fail_when_acquiring_slots_had_been_turned_off()
            {
                boundedResultsQueue.AcquireFreeResultOrWait();
                boundedResultsQueue.TurnOffAcquiringSlots();

                Action action = () => boundedResultsQueue.AcquireFreeResultOrWait();

                action.Should().Throw<InvalidOperationException>();
            }

            public void Dispose()
            {
                boundedResultsQueue?.Dispose();
            }
        }

        public class DequeResultsOnCompletionSpec : IDisposable
        {
            private readonly ITestOutputHelper output;
            private readonly BoundedResultsQueue<int> boundedResultsQueue;

            public DequeResultsOnCompletionSpec(ITestOutputHelper output)
            {
                this.output = output;
                boundedResultsQueue = new BoundedResultsQueue<int>(3, 5.Milliseconds(), new TestLog(output));
            }

            [Fact]
            public void Should_consume_with_respect_slots_order_results_completed_by_different_threads()
            {
                var expectedResults = Enumerable.Range(1, 10).ToArray();
                var producer = Task.Run(
                    () =>
                    {
                        var random = new Random();
                        for (var i = 0; i < 10; i++)
                        {
                            var slot = boundedResultsQueue.AcquireFreeResultOrWait();
                            output.WriteLine($"{DateTime.Now.TimeOfDay}. producer thrd {Thread.CurrentThread.ManagedThreadId}. acquired slot #{i}");

                            var result = i;
                            Task.Delay(random.Next(100, 301))
                                .ContinueWith(
                                    _ =>
                                    {
                                        output.WriteLine($"{DateTime.Now.TimeOfDay}. worker thrd {Thread.CurrentThread.ManagedThreadId}. complete result #{result} as {result + 1}");
                                        slot.SetResult(result + 1);
                                    });
                        }

                        output.WriteLine($"{DateTime.Now.TimeOfDay}. producer thrd {Thread.CurrentThread.ManagedThreadId} stopped.");
                    });

                List<int> actual = null;
                var consumer = Task.Factory.StartNew(
                    () =>
                    {
                        output.WriteLine($"{DateTime.Now.TimeOfDay}. consumer thrd {Thread.CurrentThread.ManagedThreadId} started.");
                        var results = new List<int>();
                        foreach (var result in boundedResultsQueue.DequeResultsOnCompletion().Take(10))
                        {
                            output.WriteLine($"{DateTime.Now.TimeOfDay}. consumer thrd {Thread.CurrentThread.ManagedThreadId}. consume result {result}");
                            results.Add(result);
                        }

                        actual = results;
                        output.WriteLine($"{DateTime.Now.TimeOfDay}. consumer thrd {Thread.CurrentThread.ManagedThreadId} stopped.");
                    },
                    TaskCreationOptions.LongRunning);

                var task = Task.WhenAll(producer, consumer);
                task.Wait(5.Seconds());

                task.IsCompleted.Should().BeTrue();

                actual.Should().BeEquivalentTo(expectedResults);
            }

            public void Dispose()
            {
                output.WriteLine("dispose");
                boundedResultsQueue?.Dispose();
            }
        }
    }
}