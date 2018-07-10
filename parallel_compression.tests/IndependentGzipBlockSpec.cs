using System;
using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using Parallel.Compression.Decompression;
using Parallel.Compression.Decompression.GzipSplitting;
using Parallel.Compression.Tests.Helpers;
using Xunit;

namespace Parallel.Compression.Tests
{
    public class IndependentGzipBlockSpec
    {
        [SuppressMessage("ReSharper", "ObjectCreationAsStatement")]
        [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
        public class Ctor
        {
            [Fact]
            public void Should_throw_when_given_null_stream()
            {
                Action action = () => new IndependentGzipBlock(null);

                action.Should().Throw<ArgumentNullException>();
            }
        }

        public class Decompress
        {
            [Fact]
            public void Should_decompress_gzipped_block_stream()
            {
                var gzipBlock = new IndependentGzipBlock(TestData.ShortFileContent.GzipCompress().AsStream());

                var decompressed = gzipBlock.Decompress();

                decompressed.AsString().Should().Be(TestData.ShortFileContent);
            }

            [Fact]
            public void Should_be_able_to_decompress_many_times()
            {
                var gzipBlock = new IndependentGzipBlock(TestData.ShortFileContent.GzipCompress().AsStream());

                var decompress1 = gzipBlock.Decompress();
                var decompress2 = gzipBlock.Decompress();

                decompress1.Should().BeEquivalentTo(decompress2);
            }
        }
    }
}