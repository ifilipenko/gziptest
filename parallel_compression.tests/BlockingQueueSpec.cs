using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Extensions;
using Parallel.Compression.Tests.Helpers;
using Parallel.Compression.Threading;
using Xunit;
using Xunit.Abstractions;

namespace Parallel.Compression.Tests
{
    public class BlockingQueueSpec
    {
        private static readonly TimeSpan ShortWait = 1500.Milliseconds();
        private static readonly TimeSpan MidWait = 5.Seconds();
        private static readonly TimeSpan LongWait = 30.Seconds();
    
        [SuppressMessage("ReSharper", "ObjectCreationAsStatement")]
        public class Ctor
        {
            [Theory]
            [InlineData(-1)]
            [InlineData(0)]
            public void Should_fail_when_given_invalid_capacity(int capacity)
            {
                Action action = () => new BlockingQueue<int>(capacity);

                action.Should().Throw<ArgumentException>();
            }

            [Fact]
            public void Should_initialize_capacity()
            {
                var blockingQueue = new BlockingQueue<int>(10);

                blockingQueue.Capacity.Should().Be(10);
            }
        }

        public class TurnOffQueueingNewElements : IDisposable
        {
            private readonly BlockingQueue<int> blockingQueue;

            public TurnOffQueueingNewElements(ITestOutputHelper output)
            {
                blockingQueue = new BlockingQueue<int>(3, new TestLog(output));
            }

            [Fact]
            public void Should_be_false_by_default()
            {
                blockingQueue.IsQueueingTurnOff.Should().BeFalse();
            }

            [Fact]
            public void Should_set_turn_off_state()
            {
                blockingQueue.TurnOffQueueingNewElements();

                blockingQueue.IsQueueingTurnOff.Should().BeTrue();
            }

            [Fact]
            public void Should_able_to_set_many_times()
            {
                blockingQueue.TurnOffQueueingNewElements();
                blockingQueue.TurnOffQueueingNewElements();
                blockingQueue.TurnOffQueueingNewElements();

                blockingQueue.IsQueueingTurnOff.Should().BeTrue();
            }

            public void Dispose()
            {
                blockingQueue?.Dispose();
            }
        }

        public class Enqueue : IDisposable
        {
            private readonly BlockingQueue<int> blockingQueue;

            public Enqueue()
            {
                blockingQueue = new BlockingQueue<int>(3);
            }

            [Fact]
            public void Should_enque_element_when_queue_does_not_rich_limit()
            {
                var allTaskCompleted = Task.WhenAll(
                    Task.Run(() => blockingQueue.Enqueue(1)),
                    Task.Run(() => blockingQueue.Enqueue(2)),
                    Task.Run(() => blockingQueue.Enqueue(3))
                );
                allTaskCompleted.Wait(ShortWait);

                allTaskCompleted.IsCompleted.Should().BeTrue();
            }

            [Fact]
            public void Should_block_current_thread_when_queue_does_rich_limit()
            {
                blockingQueue.Enqueue(1);
                blockingQueue.Enqueue(2);
                blockingQueue.Enqueue(3);

                var task = Task.Run(() => blockingQueue.Enqueue(4));
                task.Wait(ShortWait);

                task.IsCompleted.Should().BeFalse();
            }

            [Fact]
            public void Should_fail_to_enque_after_turn_off_enqueing()
            {
                blockingQueue.Enqueue(1);
                blockingQueue.TurnOffQueueingNewElements();

                Action action = () => blockingQueue.Enqueue(2);

                action.Should().Throw<InvalidOperationException>();
            }

            public void Dispose()
            {
                blockingQueue?.Dispose();
            }
        }

        public class TryDequeue : IDisposable
        {
            private readonly BlockingQueue<int> blockingQueue;

            public TryDequeue()
            {
                blockingQueue = new BlockingQueue<int>(3);
            }

            [Fact]
            public void Should_successfully_take_previous_enqueued_elements_according_to_its_order()
            {
                blockingQueue.Enqueue(1);
                blockingQueue.Enqueue(2);
                blockingQueue.Enqueue(3);

                var res1 = blockingQueue.TryDequeue();
                var res2 = blockingQueue.TryDequeue();
                var res3 = blockingQueue.TryDequeue();

                res1.IsSuccessful.Should().BeTrue();
                res1.Value.Should().Be(1);
                res2.IsSuccessful.Should().BeTrue();
                res2.Value.Should().Be(2);
                res3.IsSuccessful.Should().BeTrue();
                res3.Value.Should().Be(3);
            }

            [Fact]
            public void Should_block_thread_when_queue_is_empty()
            {
                var task = Task.Run(() => blockingQueue.TryDequeue());
                task.Wait(ShortWait);

                task.IsCompleted.Should().BeFalse();
            }

            [Fact]
            public void Should_block_thread_when_queue_became_empty()
            {
                blockingQueue.Enqueue(1);
                blockingQueue.Enqueue(2);
                blockingQueue.Enqueue(3);

                var results = new int[4];
                var expectedResults = new[] {1, 2, 3, 0};

                var task = Task.Run(
                    () =>
                    {
                        for (var i = 0; i < 4; i++)
                        {
                            results[i] = blockingQueue.TryDequeue().Value;
                        }
                    });
                task.Wait(ShortWait);

                task.IsCompleted.Should().BeFalse();
                results.Should().BeEquivalentTo(expectedResults);
            }

            [Fact]
            public void Should_not_block_thread_when_queueing_off()
            {
                blockingQueue.Enqueue(1);
                blockingQueue.Enqueue(2);
                blockingQueue.Enqueue(3);
                blockingQueue.TurnOffQueueingNewElements();

                var results = new BlockingQueue<int>.DequeueStatus[4];
                var expectedResults = new[]
                {
                    BlockingQueue<int>.DequeueStatus.Success,
                    BlockingQueue<int>.DequeueStatus.Success,
                    BlockingQueue<int>.DequeueStatus.Success,
                    BlockingQueue<int>.DequeueStatus.QueueingCancelledButQueueIsEmpty
                };

                var task = Task.Run(
                    () =>
                    {
                        for (var i = 0; i < 4; i++)
                        {
                            results[i] = blockingQueue.TryDequeue().FailureValue;
                        }
                    });
                task.Wait();
                task.IsCompleted.Should().BeTrue();

                results.Should().BeEquivalentTo(expectedResults);
            }

            [Fact]
            public void Should_unblock_thread_when_queueing_off_after_thread_is_blocked_before()
            {
                blockingQueue.Enqueue(1);
                blockingQueue.Enqueue(2);
                blockingQueue.Enqueue(3);

                var results = new BlockingQueue<int>.DequeueStatus[4];
                var expectedResults = new[]
                {
                    BlockingQueue<int>.DequeueStatus.Success,
                    BlockingQueue<int>.DequeueStatus.Success,
                    BlockingQueue<int>.DequeueStatus.Success,
                    BlockingQueue<int>.DequeueStatus.QueueingCancelledButQueueIsEmpty
                };

                var task = Task.Run(
                    () =>
                    {
                        for (var i = 0; i < 4; i++)
                        {
                            results[i] = blockingQueue.TryDequeue().FailureValue;
                        }
                    });
                task.Wait(ShortWait);
                task.IsCompleted.Should().BeFalse();

                blockingQueue.TurnOffQueueingNewElements();
                task.Wait(ShortWait);

                task.IsCompleted.Should().BeTrue();
                results.Should().BeEquivalentTo(expectedResults);
            }

            [Fact]
            public void Should_able_to_enque_and_deque_in_different_threads()
            {
                var expectedResults = new[] {1, 2, 3, 4, 5, 6, 7, 8};
                var allItems = new int[8];

                var producer = Task.Run(
                    () =>
                    {
                        blockingQueue.Enqueue(1);
                        blockingQueue.Enqueue(2);
                        blockingQueue.Enqueue(3);
                        blockingQueue.Enqueue(4);
                        blockingQueue.Enqueue(5);
                        blockingQueue.Enqueue(6);
                        blockingQueue.Enqueue(7);
                        blockingQueue.Enqueue(8);
                    });

                var consumer = Task.Run(
                    () =>
                    {
                        for (var i = 0; i < 8; i++)
                        {
                            var element = blockingQueue.TryDequeue().Value;
                            allItems[i] = element;
                        }
                    });

                var task = Task.WhenAll(producer, consumer);
                task.Wait(MidWait);

                task.IsCompleted.Should().BeTrue();
                allItems.Should().BeEquivalentTo(expectedResults);
            }

            [Fact]
            public void Should_unblock_deque_after_turn_off_enqueing()
            {
                blockingQueue.Enqueue(1);
                blockingQueue.Enqueue(2);
                blockingQueue.Enqueue(3);

                var results = new int[4];
                var expectedResults = new[] {1, 2, 3, 0};

                var task = Task.Run(
                    () =>
                    {
                        for (var i = 0; i < 4; i++)
                        {
                            results[i] = blockingQueue.TryDequeue();
                        }
                    });
                task.Wait(MidWait);

                task.IsCompleted.Should().BeFalse();
                results.Should().BeEquivalentTo(expectedResults);
            }

            public void Dispose()
            {
                blockingQueue?.Dispose();
            }
        }

        public class TryDequeIfHeadMatched : IDisposable
        {
            private readonly ITestOutputHelper output;
            private readonly BlockingQueue<int> blockingQueue;

            public TryDequeIfHeadMatched(ITestOutputHelper output)
            {
                this.output = output;
                blockingQueue = new BlockingQueue<int>(5);
            }

            [Fact]
            [SuppressMessage("ReSharper", "IteratorMethodResultIsIgnored")]
            [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
            [SuppressMessage("ReSharper", "ReturnValueOfPureMethodIsNotUsed")]
            public void Should_fail_when_given_null_condition()
            {
                Action action = () => blockingQueue.TryDequeIfHeadMatched(null, out _);

                action.Should().Throw<ArgumentNullException>();
            }

            [Fact]
            public void Should_deque_elements_while_they_match_to_condition()
            {
                blockingQueue.Enqueue(1);
                blockingQueue.Enqueue(2);
                blockingQueue.Enqueue(3);
                blockingQueue.Enqueue(4);
                blockingQueue.Enqueue(5);

                var expectedResults = new[] {1, 2, 3, 0, 0};
                var consumedItems = new int[5];
                var consumed = 0;

                var consumer = Task.Run(
                    () =>
                    {
                        var i = 0;
                        while (blockingQueue.TryDequeIfHeadMatched(x => x < 4, out var item))
                        {
                            consumed++;
                            consumedItems[i++] = item;
                        }
                    });

                consumer.Wait(ShortWait);

                consumer.IsCompleted.Should().BeTrue();
                consumed.Should().Be(3);
                consumedItems.Should().BeEquivalentTo(expectedResults);
            }

            [Fact]
            public void Should_block_thread_when_queue_is_empty()
            {
                var task = Task.Run(() => blockingQueue.TryDequeIfHeadMatched(x => x < 4, out _));
                task.Wait(ShortWait);

                task.IsCompleted.Should().BeFalse();
            }

            [Fact]
            public void Should_block_thread_when_queue_became_empty()
            {
                blockingQueue.Enqueue(1);
                blockingQueue.Enqueue(2);
                blockingQueue.Enqueue(3);
                blockingQueue.Enqueue(4);
                blockingQueue.Enqueue(5);

                var expectedResults = new[] {1, 2, 3, 4, 5};
                var allItems = new int[5];

                var task = Task.Run(
                    () =>
                    {
                        var i = 0;
                        while (blockingQueue.TryDequeIfHeadMatched(x => true, out var item))
                        {
                            allItems[i++] = item;
                        }
                    });
                task.Wait(ShortWait);

                task.IsCompleted.Should().BeFalse();
                allItems.Should().BeEquivalentTo(expectedResults);
            }

            [Fact]
            public void Should_able_to_enque_and_dequeue_in_different_threads()
            {
                var expectedResults = new[] {1, 2, 3, 4, 5, 6, 7, 8};
                int[] allItems = null;

                var producer = Task.Run(
                    () =>
                    {
                        blockingQueue.Enqueue(1);
                        blockingQueue.Enqueue(2);
                        blockingQueue.Enqueue(3);
                        blockingQueue.Enqueue(4);
                        blockingQueue.Enqueue(5);
                        blockingQueue.Enqueue(6);
                        blockingQueue.Enqueue(7);
                        blockingQueue.Enqueue(8);
                    });

                var consumer = Task.Run
                (
                    () =>
                    {
                        var items = new int[8];
                        for (var i = 0; i < 8; i++)
                        {
                            var result = blockingQueue.TryDequeIfHeadMatched(x => true, out var item);
                            output.WriteLine($"consumed item {item} as #{i} with result {result}");
                            items[i] = item;
                        }

                        allItems = items;
                    });

                var task = Task.WhenAll(producer, consumer);
                task.Wait(LongWait);

                PrintAllItems(allItems);

                task.IsCompleted.Should().BeTrue();
                allItems.Should().BeEquivalentTo(expectedResults);
            }

            [Fact]
            [SuppressMessage("ReSharper", "AccessToDisposedClosure")]
            public void Should_able_unblock_when_item_matched_to_condition_after_some_delay()
            {
                using (var queue = new BlockingQueue<DelayedItem>(5))
                {
                    var expectedResults = Enumerable.Range(1, 10).ToArray();
                    int[] allItems = null;

                    var producer = Task.Run(
                        () =>
                        {
                            for (var i = 0; i < 10; i++)
                            {
                                var item = new DelayedItem("delay value", i + 1, i%2 == 0 ? 5 : 0);
                                queue.Enqueue(item);
                                output.WriteLine($"Enqueue {item.ResultAfterDelay}");
                            }
                        });
                    var consumer = Task.Run(
                        () =>
                        {
                            var items = new List<int>();
                            while (items.Count < 10)
                            {
                                if (queue.TryDequeIfHeadMatched(x => x.Result is int, out var item))
                                {
                                    output.WriteLine($"Consumer checks result {item.ResultChecksCount} times");
                                    items.Add((int) item.Result);
                                }
                            }

                            allItems = items.ToArray();
                        });

                    var task = Task.WhenAll(producer, consumer);
                    task.Wait(LongWait);

                    task.IsCompleted.Should().BeTrue();
                    PrintAllItems(allItems);
                    allItems.Should().HaveSameCount(expectedResults);
                    allItems.Should().BeEquivalentTo(expectedResults);
                }
            }

            [Fact]
            public void Should_able_to_enque_and_dequeue_from_many_threads()
            {
                var expectedResults = Enumerable.Range(1, 24).ToArray();
                int[] allItems = null;

                var producers = new[]
                {
                    Task.Run(() => ProduceItems(1, 8)),
                    Task.Run(() => ProduceItems(9, 8)),
                    Task.Run(() => ProduceItems(17, 8))
                };
                var consumers = new[]
                {
                    Task.Factory.StartNew(() => DequeueAnyForGivenTimes(8), TaskCreationOptions.LongRunning),
                    Task.Factory.StartNew(() => DequeueAnyForGivenTimes(8), TaskCreationOptions.LongRunning),
                    Task.Factory.StartNew(() => DequeueAnyForGivenTimes(8), TaskCreationOptions.LongRunning)
                };

                var consumersResults = Task.WhenAll(consumers).ContinueWith(x => allItems = x.Result.SelectMany(item => item).ToArray());
                var task = Task.WhenAll(new List<Task>(producers) {consumersResults});
                task.Wait(LongWait);

                consumersResults.IsCompleted.Should().BeTrue();
                PrintAllItems(allItems);
                allItems.Should().HaveSameCount(expectedResults);
                allItems.Should().Contain(expectedResults);

                int[] DequeueAnyForGivenTimes(int count)
                {
                    output.WriteLine($"{DateTime.Now.TimeOfDay}. new consume thrd {Thread.CurrentThread.ManagedThreadId}");
                    var items = new int[count];
                    var i = 0;
                    while (i < count)
                    {
                        if (blockingQueue.TryDequeIfHeadMatched(x => true, out var item))
                        {
                            items[i++] = item;
                            output.WriteLine($"{DateTime.Now.TimeOfDay}. consumer thrd {Thread.CurrentThread.ManagedThreadId} <= {item}");
                        }
                    }

                    output.WriteLine($"{DateTime.Now.TimeOfDay}. consumer thrd {Thread.CurrentThread.ManagedThreadId} stopped");
                    return items;
                }

                void ProduceItems(int start, int count)
                {
                    output.WriteLine($"{DateTime.Now.TimeOfDay}. new produce thrd {Thread.CurrentThread.ManagedThreadId}");
                    for (var i = 0; i < count; i++)
                    {
                        output.WriteLine($"{DateTime.Now.TimeOfDay}. producer thrd {Thread.CurrentThread.ManagedThreadId} => {i + start}");
                        blockingQueue.Enqueue(i + start);
                    }

                    output.WriteLine($"{DateTime.Now.TimeOfDay}. producer thrd {Thread.CurrentThread.ManagedThreadId} stopped");
                }
            }

            private void PrintAllItems<T>(IEnumerable<T> allItems)
            {
                output.WriteLine($"print thrd {Thread.CurrentThread.ManagedThreadId}: all items {string.Join(", ", allItems)}");
            }

            public void Dispose()
            {
                blockingQueue?.Dispose();
            }

            private class DelayedItem
            {
                private readonly object resultBeforeDelay;
                private readonly int resultAfterDelay;
                private int resultCheckSwitchTimes;
                private int resultChecksCount;

                public DelayedItem(object resultBeforeDelay, int resultAfterDelay, int resultCheckSwitchTimes)
                {
                    this.resultBeforeDelay = resultBeforeDelay;
                    this.resultAfterDelay = resultAfterDelay;
                    this.resultCheckSwitchTimes = resultCheckSwitchTimes;
                }

                public object Result
                {
                    get
                    {
                        resultChecksCount++;

                        if (resultCheckSwitchTimes > 0)
                        {
                            resultCheckSwitchTimes--;
                            return resultBeforeDelay;
                        }

                        return resultAfterDelay;
                    }
                }

                public int ResultChecksCount => resultChecksCount;
                public object ResultAfterDelay => resultAfterDelay;
            }
        }

        public class Size : IDisposable
        {
            private readonly ITestOutputHelper output;
            private readonly BlockingQueue<int> blockingQueue;

            public Size(ITestOutputHelper output)
            {
                this.output = output;
                blockingQueue = new BlockingQueue<int>(3);
            }

            [Fact]
            public void Should_be_0_when_queue_is_empty()
            {
                blockingQueue.Size.Should().Be(0);
            }

            [Fact]
            public void Should_update_when_different_threads_produced_and_consumed_items()
            {
                var producerSizes = new List<int>();
                var producer = Task.Run(
                    () =>
                    {
                        for (var i = 0; i < 8; i++)
                        {
                            blockingQueue.Enqueue(i + 1);
                            producerSizes.Add(blockingQueue.Size);
                        }
                    });

                var consumerSizes = new List<int>();
                var consumer = Task.Run(
                    () =>
                    {
                        for (var i = 0; i < 8; i++)
                        {
                            blockingQueue.Dequeue();
                            consumerSizes.Add(blockingQueue.Size);
                        }
                    });

                var task = Task.WhenAll(producer, consumer);
                task.Wait(LongWait);

                task.IsCompleted.Should().BeTrue();
                PrintSizes();
                producerSizes.Should().NotBeEquivalentTo(consumerSizes).And.HaveSameCount(consumerSizes);

                void PrintSizes()
                {
                    output.WriteLine("producer sizes: " + string.Join(", ", producerSizes));
                    output.WriteLine("consumer sizes: " + string.Join(", ", consumerSizes));
                }
            }

            public void Dispose()
            {
                blockingQueue?.Dispose();
            }
        }

        public class IsFull : IDisposable
        {
            private readonly BlockingQueue<int> blockingQueue;

            public IsFull()
            {
                blockingQueue = new BlockingQueue<int>(3);
            }

            [Fact]
            public void Should_be_false_when_queue_is_empty()
            {
                blockingQueue.IsFull.Should().BeFalse();
            }

            [Fact]
            public void Should_be_full_when_enqueue_max_elements()
            {
                blockingQueue.Enqueue(1);
                blockingQueue.Enqueue(2);
                blockingQueue.Enqueue(3);

                blockingQueue.IsFull.Should().BeTrue();
            }

            [Fact]
            public void Should_be_reset_when_dequeue_at_least_an_element_from_full_queue()
            {
                blockingQueue.Enqueue(1);
                blockingQueue.Enqueue(2);
                blockingQueue.Enqueue(3);

                blockingQueue.IsFull.Should().BeTrue();

                blockingQueue.Dequeue();
               
                blockingQueue.IsFull.Should().BeFalse();

                blockingQueue.Dequeue();
                
                blockingQueue.IsFull.Should().BeFalse();
            }

            public void Dispose()
            {
                blockingQueue?.Dispose();
            }
        }
    }
}