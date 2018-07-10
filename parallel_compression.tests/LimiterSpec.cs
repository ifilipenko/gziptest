using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Parallel.Compression.Logging;
using Parallel.Compression.Tests.Helpers;
using Parallel.Compression.Threading;
using Xunit;
using Xunit.Abstractions;

namespace Parallel.Compression.Tests
{
    public class LimiterSpec
    {
        [SuppressMessage("ReSharper", "ObjectCreationAsStatement")]
        public class Ctor
        {
            [Fact]
            public void Should_fail_when_given_invalid_free_value()
            {
                Action action = () => new Limiter(-1);

                action.Should().Throw<ArgumentException>();
            }

            [Theory]
            [InlineData(-1)]
            [InlineData(0)]
            public void Should_fail_when_given_invalid_max_value(int value)
            {
                Action action = () => new Limiter(0, value);

                action.Should().Throw<ArgumentException>();
            }

            [Fact]
            public void Should_fail_when_given_free_value_greater_than_max_value()
            {
                Action action = () => new Limiter(2, 1);

                action.Should().Throw<ArgumentException>();
            }

            [Fact]
            public void Should_initialize_with_current_free_and_max_values()
            {
                var limiter = new Limiter(10, 11);

                limiter.CurrentFreeNumber.Should().Be(10);
                limiter.MaxLimit.Should().Be(11);
            }

            [Fact]
            public void Should_set_max_limit_to_max_integer_by_default()
            {
                var limiter = new Limiter(10);

                limiter.CurrentFreeNumber.Should().Be(10);
                limiter.MaxLimit.Should().Be(int.MaxValue);
            }
        }

        public class TryRelease
        {
            [Theory]
            [InlineData(0)]
            [InlineData(-1)]
            public void Should_throw_when_try_to_release_invalid_number(int value)
            {
                var limiter = new Limiter(10);
                
                Action action = () => limiter.TryRelease(value, out _);

                action.Should().Throw<ArgumentException>();
            }

            [Theory]
            [InlineData(10, 1, 10)]
            [InlineData(10, 2, 10)]
            [InlineData(10, 10, 10)]
            public void Should_return_old_value(int count, int release, int expectedValue)
            {
                var limiter = new Limiter(count);

                limiter.TryRelease(release,out var oldValue);

                oldValue.Should().Be(expectedValue);
            }
            
            [Fact]
            public void Should_return_true_when_is_have_free_numbers()
            {
                var limiter = new Limiter(10);

                var result = limiter.TryRelease(5, out _);

                result.Should().BeTrue();
            }

            [Fact]
            public void Should_increase_current_value()
            {
                var limiter = new Limiter(10);

                limiter.TryRelease(5, out _);

                limiter.CurrentFreeNumber.Should().Be(15);
            }

            [Fact]
            public void Should_return_false_when_given_count_more_than_have_free()
            {
                var limiter = new Limiter(10, 11);

                var result = limiter.TryRelease(5, out _);

                result.Should().BeFalse();
            }

            [Fact]
            public void Should_return_current_value_when_given_count_more_than_have_free()
            {
                var limiter = new Limiter(10, 11);

                limiter.TryRelease(5, out var current);

                current.Should().Be(10);
            }

            [Fact]
            public void Should_not_release_when_given_count_more_than_have_free()
            {
                var limiter = new Limiter(10, 11);

                limiter.TryRelease(5, out _);

                limiter.CurrentFreeNumber.Should().Be(10);
            }
        }

        public class WaitFree
        {
            private TestLog log;

            public WaitFree(ITestOutputHelper output)
            {
                log = new TestLog(output);
            }

            [Fact]
            public void Should_decrease_current_value()
            {
                var limiter = new Limiter(10, log: log);

                limiter.WaitFree();

                limiter.CurrentFreeNumber.Should().Be(9);
            }

            [Fact]
            public void Should_block_current_thread_when_no_free_values()
            {
                var limiter = new Limiter(0, log: log);

                var task = Task.Run(() => limiter.WaitFree());
                task.Wait(300);

                task.IsCanceled.Should().BeFalse();
            }
        }

        public class ReleaseForever
        {
            [Fact]
            public void Should_set_release_into_max_value()
            {
                var limiter = new Limiter(0, 10);

                limiter.ReleaseForever();

                limiter.CurrentFreeNumber.Should().Be(10);
            }

            [Fact]
            public void Should_not_allow_to_decreases_by_waits()
            {
                var limiter = new Limiter(0, 2);

                limiter.ReleaseForever();

                limiter.WaitFree();
                limiter.WaitFree();
                limiter.WaitFree();
                limiter.CurrentFreeNumber.Should().Be(2);
            }
        }

        public class Releases_and_Waits
        {
            private ILog log;

            public Releases_and_Waits(ITestOutputHelper output)
            {
                log = new TestLog(output);
            }

            [Fact]
            public void Should_block_wait_thread_while_not_relese_enough_counter()
            {
                var limiter = new Limiter(0, 10);
                
                var waitTasks = new []
                {
                    new Task(() => limiter.WaitFree(),TaskCreationOptions.LongRunning),
                    new Task(() => limiter.WaitFree(),TaskCreationOptions.LongRunning),
                    new Task(() => limiter.WaitFree(),TaskCreationOptions.LongRunning)
                };

                Start(waitTasks);
                var whenAll = Task.WhenAll(waitTasks);
                var whenAny = Task.WhenAny(waitTasks);
                whenAll.Wait(200);
                whenAll.IsCompleted.Should().BeFalse();

                limiter.TryRelease(out _);
                
                whenAny.Wait(100);
                whenAny.IsCompleted.Should().BeTrue();
                whenAll.IsCompleted.Should().BeFalse();
                
                limiter.TryRelease(2, out _);
                whenAll.Wait(100);
                whenAll.IsCompleted.Should().BeTrue();

                limiter.CurrentFreeNumber.Should().Be(0);
            }
            
            [Fact]
            public void Should_unblock_blocked_wait_thread_when_release_forever()
            {
                var limiter = new Limiter(0, 10, log);
                
                var waitTasks = new []
                {
                    new Task(() => limiter.WaitFree(),TaskCreationOptions.LongRunning),
                    new Task(() => limiter.WaitFree(),TaskCreationOptions.LongRunning),
                    new Task(() => limiter.WaitFree(),TaskCreationOptions.LongRunning)
                };

                Start(waitTasks);
                var whenAll = Task.WhenAll(waitTasks);
                var whenAny = Task.WhenAny(waitTasks);
                whenAll.Wait(200);
                whenAll.IsCompleted.Should().BeFalse();

                limiter.ReleaseForever();
                
                whenAny.Wait(100);
                whenAny.IsCompleted.Should().BeTrue();
                whenAll.Wait(100);
                whenAll.IsCompleted.Should().BeTrue();

                limiter.CurrentFreeNumber.Should().Be(10);
            }
            
            [Fact]
            public void Should_wait_and_release_by_one_number_from_different_threads()
            {
                var limiter = new Limiter(0, 10, log);
                
                var waitTask = new Task(
                    () =>
                    {
                        for (var i = 0; i < 100; i++)
                        {
                            limiter.WaitFree();
                        }
                    },
                    TaskCreationOptions.LongRunning);

                var releaseTask = new Task(
                    () =>
                    {
                        var released = 100;
                        while (released > 0)
                        {
                            if (limiter.TryRelease(1, out _))
                            {
                                released -= 1;
                            }
                            Thread.SpinWait(3);
                        }
                    },
                    TaskCreationOptions.LongRunning);

                Start(waitTask, releaseTask);
                var whenAll = Task.WhenAll(waitTask, releaseTask);
                whenAll.Wait(200);
                
                whenAll.IsCompleted.Should().BeTrue();

                limiter.CurrentFreeNumber.Should().Be(0);
            }
            
            [Fact]
            public void Should_wait_and_release_by_some_number_from_different_threads()
            {
                var limiter = new Limiter(0, 10, log);
                
                var waitTask = new Task(
                    () =>
                    {
                        for (var i = 0; i < 100; i++)
                        {
                            limiter.WaitFree();
                        }
                    },
                    TaskCreationOptions.LongRunning);

                var releaseTask = new Task(
                    () =>
                    {
                        var released = 100;
                        var i = 0;
                        while (released > 0)
                        {
                            var times = i++%3 + 1;
                            if (limiter.TryRelease(times, out _))
                            {
                                released -= times;
                            }
                            Thread.SpinWait(times * 3);
                        }
                    },
                    TaskCreationOptions.LongRunning);

                Start(waitTask, releaseTask);
                var whenAll = Task.WhenAll(waitTask, releaseTask);
                whenAll.Wait(200);
                
                whenAll.IsCompleted.Should().BeTrue();

                limiter.CurrentFreeNumber.Should().BeLessOrEqualTo(10);
            }
            
            [Fact]
            public void Should_wait_and_release_by_some_number_from_many_threads()
            {
                var limiter = new Limiter(0, 10, log);
                
                var waitTask = new []
                {
                    new Task(() => WaitFor(limiter, 10),TaskCreationOptions.LongRunning),
                    new Task(() => WaitFor(limiter, 10),TaskCreationOptions.LongRunning),
                    new Task(() => WaitFor(limiter, 10),TaskCreationOptions.LongRunning)
                };

                var releaseTask = new[]
                {
                    new Task(() => ReleaseFor(limiter, 10), TaskCreationOptions.LongRunning),
                    new Task(() => ReleaseFor(limiter, 10), TaskCreationOptions.LongRunning),
                    new Task(() => ReleaseFor(limiter, 10), TaskCreationOptions.LongRunning)
                };

                var tasks = waitTask.Concat(releaseTask).ToArray();
                Start(tasks);
                var whenAll = Task.WhenAll(tasks);
                whenAll.Wait(200);
                whenAll.IsCompleted.Should().BeTrue();

                limiter.CurrentFreeNumber.Should().BeLessOrEqualTo(10);
            }
            
            [Fact]
            public void Should_wait_and_release_by_one_number_from_many_threads()
            {
                var limiter = new Limiter(0, 10, log);
                
                var waitTask = new []
                {
                    new Task(() => WaitFor(limiter, 10),TaskCreationOptions.LongRunning),
                    new Task(() => WaitFor(limiter, 10),TaskCreationOptions.LongRunning),
                    new Task(() => WaitFor(limiter, 10),TaskCreationOptions.LongRunning)
                };

                var releaseTask = new[]
                {
                    new Task(() => ReleaseFor(limiter, 10, 1), TaskCreationOptions.LongRunning),
                    new Task(() => ReleaseFor(limiter, 10, 1), TaskCreationOptions.LongRunning),
                    new Task(() => ReleaseFor(limiter, 10, 1), TaskCreationOptions.LongRunning)
                };

                var tasks = waitTask.Concat(releaseTask).ToArray();
                Start(tasks);
                var whenAll = Task.WhenAll(tasks);
                whenAll.Wait(200);
                whenAll.IsCompleted.Should().BeTrue();

                limiter.CurrentFreeNumber.Should().BeLessOrEqualTo(10);
            }

            private static void WaitFor(Limiter limiter, int times)
            {
                for (var i = 0; i < times; i++)
                {
                    limiter.WaitFree();
                }
            }

            private void ReleaseFor(Limiter limiter, int times, int? step = null)
            {
                var released = 0;
                var i = 0;
                while (released < times)
                {
                    int currentTimes;
                    
                    if (step.HasValue)
                    {
                        currentTimes = step.Value;
                    }
                    else
                    {
                        currentTimes = i++%3 + 1;
                        if (currentTimes + released > times)
                        {
                            currentTimes = 1;
                        }
                    }

                    if (limiter.TryRelease(currentTimes, out _))
                    {
                        released += currentTimes;
                    }
                }
            }

            private static void Start(params Task[] notRunnedTasks)
            {
                foreach (var task in notRunnedTasks)
                {
                    task.Status.Should().Be(TaskStatus.Created);
                }

                foreach (var task in notRunnedTasks)
                {
                    task.Start(TaskScheduler.Default);
                }
            }
        }
    }
}