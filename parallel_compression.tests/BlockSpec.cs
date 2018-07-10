using System;
using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using Parallel.Compression.Models;
using Parallel.Compression.Tests.Helpers;
using Xunit;

namespace Parallel.Compression.Tests
{
    public class BlockSpec
    {
        public class Default
        {
            private readonly Block block = default(Block);

            [Fact]
            public void Should_return_empty_bytes()
            {
                block.Bytes.Should().BeEquivalentTo(new ArraySegment<byte>(new byte[0]));
            }

            [Fact]
            public void Should_have_zero_offset()
            {
                block.Offset.Should().Be(0);
            }
        }

        [SuppressMessage("ReSharper", "ObjectCreationAsStatement")]
        public class Ctor
        {
            [Fact]
            public void Should_fail_when_given_negative_offset()
            {
                Action action = () => new Block(new ArraySegment<byte>(new byte[100]), -1);

                action.Should().Throw<ArgumentException>();
            }

            [Fact]
            public void Should_initialize_with_given_parameters()
            {
                var block = new Block("blah".ToArraySegment(), 100);

                block.Bytes.Should().BeEquivalentTo("blah".ToArraySegment());
                block.Offset.Should().Be(100);
            }

            [Fact]
            public void Should_set_empty_array_segment_when_given_default_bytes_segment()
            {
                var block = new Block(default(ArraySegment<byte>), 100);

                block.Bytes.Should().BeEquivalentTo(new ArraySegment<byte>(new byte[0]));
                block.Offset.Should().Be(100);
            }
        }

        public class IsEmpty
        {
            [Fact]
            public void Should_return_true_when_bytes_is_empty()
            {
                var block = new Block(new byte[0], 100);

                block.IsEmpty.Should().BeTrue();
            }

            [Fact]
            public void Should_return_false_when_bytes_is_not_empty()
            {
                var block = new Block(new byte[10], 100);

                block.IsEmpty.Should().BeFalse();
            }
        }
    }
}