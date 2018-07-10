using System;
using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using Parallel.Compression.Models;
using Xunit;

namespace Parallel.Compression.Tests
{
    public class ByteMaskSpec
    {
        public class Default
        {
            private ByteMask mask = default;

            [Theory]
            [InlineData((byte) 0)]
            [InlineData((byte) 11)]
            [InlineData((byte) 255)]
            public void Should_not_match_to_anythging(byte value)
            {
                mask.IsMatched(value).Should().BeFalse();
            }
        }

        public class Any
        {
            private ByteMask mask = ByteMask.Any;

            [Theory]
            [InlineData((byte) 0)]
            [InlineData((byte) 11)]
            [InlineData((byte) 255)]
            public void Should_match_to_any_bytes(byte value)
            {
                mask.IsMatched(value).Should().BeTrue();
            }
        }

        public class Between
        {
            [Fact]
            public void Should_fail_when_min_is_greater_than_max()
            {
                Action action = () => ByteMask.Between(128, 8);

                action.Should().Throw<ArgumentException>();
            }

            [Fact]
            public void Should_match_bytes_that_in_range_of_given_bounds()
            {
                var mask = ByteMask.Between(8, 128);

                mask.IsMatched(8).Should().BeTrue();
                mask.IsMatched(128).Should().BeTrue();
                mask.IsMatched(100).Should().BeTrue();
            }

            [Fact]
            public void Should_not_match_bytes_that_out_of_range_of_given_bounds()
            {
                var mask = ByteMask.Between(8, 128);

                mask.IsMatched(7).Should().BeFalse();
                mask.IsMatched(129).Should().BeFalse();
            }
        }

        public class Values
        {
            [Fact]
            public void Should_fail_when_given_null_array()
            {
                Action action = () => ByteMask.Values(null);

                action.Should().Throw<ArgumentNullException>();
            }

            [Fact]
            public void Should_match_bytes_that_in_contains_in_given_values_list()
            {
                var mask = ByteMask.Values(new byte[] {1, 2, 3});

                mask.IsMatched(1).Should().BeTrue();
                mask.IsMatched(2).Should().BeTrue();
                mask.IsMatched(3).Should().BeTrue();
            }

            [Fact]
            public void Should_not_match_bytes_that_not_contains_in_values_list()
            {
                var mask = ByteMask.Values(new byte[] {1, 2, 3});

                mask.IsMatched(4).Should().BeFalse();
                mask.IsMatched(0).Should().BeFalse();
            }

            [Fact]
            public void Should_be_able_to_initialize_from_byte_array()
            {
                ByteMask mask = new byte[] {1, 2, 3};

                mask.IsMatched(1).Should().BeTrue();
                mask.IsMatched(2).Should().BeTrue();
                mask.IsMatched(3).Should().BeTrue();
                mask.IsMatched(4).Should().BeFalse();
                mask.IsMatched(0).Should().BeFalse();
            }

            [Fact]
            [SuppressMessage("ReSharper", "ExpressionIsAlwaysNull")]
            [SuppressMessage("ReSharper", "UnusedVariable")]
            public void Should_fail_when_initialize_from_null_array()
            {
                byte[] bytes = null;
                Action action = () =>
                {
                    ByteMask mask = bytes;
                };

                action.Should().Throw<ArgumentNullException>();
            }
        }

        public class Exact
        {
            [Fact]
            public void Should_match_equal_bytes()
            {
                var mask = ByteMask.Exact(3);

                mask.IsMatched(3).Should().BeTrue();
            }

            [Fact]
            public void Should_not_match_not_equal_bytes()
            {
                var mask = ByteMask.Exact(3);

                mask.IsMatched(2).Should().BeFalse();
            }

            [Fact]
            public void Should_be_initializable_from_byte()
            {
                ByteMask mask = (byte) 3;

                mask.IsMatched(3).Should().BeTrue();
                mask.IsMatched(2).Should().BeFalse();
            }

            [Fact]
            public void Should_be_initializable_from_int()
            {
                ByteMask mask = 3;

                mask.IsMatched(3).Should().BeTrue();
                mask.IsMatched(2).Should().BeFalse();
            }
        }
    }
}