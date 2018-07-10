using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using FluentAssertions;
using NSubstitute;
using Parallel.Compression.Compression;
using Parallel.Compression.Decompression;
using Parallel.Compression.Decompression.GzipSplitting;
using Parallel.Compression.Decompression.Streams;
using Parallel.Compression.GzipFormat;
using Parallel.Compression.Logging;
using Parallel.Compression.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Parallel.Compression.Tests
{
    public class MimeTypeLengthGzipSplitterSpec
    {
        [SuppressMessage("ReSharper", "ObjectCreationAsStatement")]
        [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
        public class Ctor
        {
            [Theory]
            [InlineData(-1)]
            [InlineData(0)]
            public void Should_throw_when_given_invalid_independency_buffer_size(int bufferSize)
            {
                Action action = () => new MimeTypeLengthGzipSplitter(bufferSize, Substitute.For<ILog>());

                action.Should().Throw<ArgumentOutOfRangeException>();
            }

            [Fact]
            public void Should_fail_when_given_null_logger()
            {
                Action action = () => new MimeTypeLengthGzipSplitter(128, null);

                action.Should().Throw<ArgumentNullException>();
            }
        }

        public class SplitToIndependentBlocks
        {
            public enum BrokenLengthCases
            {
                WithoutLength,
                LessThanExpected,
                GreaterThanExpected
            }

            private readonly ILog log;

            public SplitToIndependentBlocks(ITestOutputHelper output)
            {
                log = new TestLog(output);
            }

            [Fact]
            public void Should_return_a_block_when_it_compressed_with_length_in_header_and_equal_to_buffer_limit()
            {
                var compressed = TestData.ShortFileContent.CompessWithDecompressionHelp(DecompressionHelpMode.BlockLengthInMimetypeSection);
                var stream = compressed.ToArray().AsStream();
                var splitter = new MimeTypeLengthGzipSplitter(compressed.Length, log);
                var rewindableStream = new RewindableReadonlyStream(stream);

                var blocks = splitter.SplitToIndependentBlocks(rewindableStream).ToArray();

                blocks.Should().HaveCount(2);
                blocks[0].HasBlock.Should().BeTrue();
                blocks[1].Status.Should().Be(GzipSplittingStatus.StreamIsEnd);
            }

            [Fact]
            public void Should_return_a_block_when_it_compressed_with_length_in_header_and_smaller_than_buffer_limit()
            {
                var compressed = TestData.ShortFileContent.CompessWithDecompressionHelp(DecompressionHelpMode.BlockLengthInMimetypeSection);
                var stream = compressed.ToArray().AsStream();
                var splitter = new MimeTypeLengthGzipSplitter(compressed.Length*2, log);
                var rewindableStream = new RewindableReadonlyStream(stream);

                var blocks = splitter.SplitToIndependentBlocks(rewindableStream).ToArray();

                blocks.Should().HaveCount(2);
                blocks[0].HasBlock.Should().BeTrue();
                blocks[1].Status.Should().Be(GzipSplittingStatus.StreamIsEnd);
            }

            [Fact]
            public void Should_split_into_blocks_when_compressed_blocks_has_length_in_headers_and_blocks_smaller_than_buffer_limit()
            {
                var compressed = TestData.ShortFileContent.CompessWithDecompressionHelp(DecompressionHelpMode.BlockLengthInMimetypeSection);
                var stream = Enumerable.Range(0, 5).SelectMany(_ => compressed).ToArray().AsStream();
                var splitter = new MimeTypeLengthGzipSplitter(compressed.Length*2, log);
                var rewindableStream = new RewindableReadonlyStream(stream);

                var blocks = splitter.SplitToIndependentBlocks(rewindableStream).ToArray();

                blocks.Should().HaveCount(6);
                blocks.Take(5).Should().OnlyContain(x => x.HasBlock);
                blocks[5].Status.Should().Be(GzipSplittingStatus.StreamIsEnd);
            }

            [Fact]
            public void Should_not_left_some_buffers_in_stream_when_compressed_blocks_has_length_in_headers_and_blocks_smaller_than_buffer_limit()
            {
                var compressed = TestData.ShortFileContent.CompessWithDecompressionHelp(DecompressionHelpMode.BlockLengthInMimetypeSection);
                var stream = Enumerable.Range(0, 5).SelectMany(_ => compressed).ToArray().AsStream();
                var splitter = new MimeTypeLengthGzipSplitter(compressed.Length*2, log);
                var rewindableStream = new RewindableReadonlyStream(stream);

                splitter.SplitToIndependentBlocks(rewindableStream).Enumerate();

                rewindableStream.ReturnedBuffer.Should().HaveCount(0);
            }

            [Theory]
            [InlineData(BrokenLengthCases.WithoutLength)]
            [InlineData(BrokenLengthCases.GreaterThanExpected)]
            [InlineData(BrokenLengthCases.LessThanExpected)]
            public void Should_not_to_split_stream_and_return_used_bytes_when_first_block_have_invalid_length(BrokenLengthCases theCase)
            {
                var wrongLengthInHeader = TestData.ShortFileContent.CompessWithDecompressionHelp(DecompressionHelpMode.BlockLengthInMimetypeSection);
                var expectedReadedBytes = SetLengthToHeader(wrongLengthInHeader, theCase);
                var withLengthInHeader = TestData.ShortFileContent.CompessWithDecompressionHelp(DecompressionHelpMode.BlockLengthInMimetypeSection);

                var bytes = Enumerable.Range(0, 5)
                    .Select(_ => withLengthInHeader)
                    .Prepend(wrongLengthInHeader)
                    .SelectMany(x => x)
                    .ToArray();
                var stream = bytes.AsStream();
                var splitter = new MimeTypeLengthGzipSplitter(int.MaxValue, log);
                var rewindableStream = new RewindableReadonlyStream(stream);

                var blocks = splitter.SplitToIndependentBlocks(rewindableStream).ToArray();

                blocks.Should().HaveCount(1);
                blocks[0].Status.Should().Be(GzipSplittingStatus.CantReadBlock);
                rewindableStream.ReturnedBuffer.Should().HaveCount(expectedReadedBytes);
                rewindableStream.ReturnedBuffer.Should().BeEquivalentTo(bytes.Take(expectedReadedBytes).ToArray());
            }

            [Theory]
            [InlineData(BrokenLengthCases.WithoutLength)]
            [InlineData(BrokenLengthCases.GreaterThanExpected)]
            [InlineData(BrokenLengthCases.LessThanExpected)]
            public void Should_stop_and_return_used_bytes_when_some_of_block_have_invalid_length(BrokenLengthCases theCase)
            {
                var wrongLengthInHeader = TestData.ShortFileContent.CompessWithDecompressionHelp(DecompressionHelpMode.BlockLengthInMimetypeSection);
                var expectedReadedBytes = SetLengthToHeader(wrongLengthInHeader, theCase);
                var withLengthInHeader = TestData.ShortFileContent.CompessWithDecompressionHelp(DecompressionHelpMode.BlockLengthInMimetypeSection);

                var bytes = Enumerable.Range(0, 3)
                    .Select(_ => withLengthInHeader)
                    .Append(wrongLengthInHeader)
                    .Append(withLengthInHeader)
                    .SelectMany(x => x)
                    .ToArray();
                var stream = bytes.AsStream();
                var splitter = new MimeTypeLengthGzipSplitter(int.MaxValue, log);
                var rewindableStream = new RewindableReadonlyStream(stream);

                var blocks = splitter.SplitToIndependentBlocks(rewindableStream).ToArray();

                blocks.Should().HaveCount(4);
                blocks.Take(3).Should().OnlyContain(x => x.HasBlock);
                blocks[3].Status.Should().Be(GzipSplittingStatus.CantReadBlock);
                rewindableStream.ReturnedBuffer.Should().HaveCount(expectedReadedBytes);
                rewindableStream.ReturnedBuffer.Should().BeEquivalentTo(bytes.Skip(withLengthInHeader.Length*3).Take(expectedReadedBytes).ToArray());
            }

            [Fact]
            public void Should_split_into_correctly_decomressable_independent_blocks()
            {
                var expectedBlockStrings = Enumerable.Range(0, 5)
                    .Select(x => x + " " + TestData.ShortFileContent.Shuffle())
                    .ToArray();

                var stream = expectedBlockStrings.SelectMany(x => x.CompessWithDecompressionHelp(DecompressionHelpMode.BlockLengthInMimetypeSection)).ToArray().AsStream();
                var splitter = new MimeTypeLengthGzipSplitter(int.MaxValue, log);
                var rewindableStream = new RewindableReadonlyStream(stream);

                var blocks = splitter.SplitToIndependentBlocks(rewindableStream).ToArray();

                blocks.Should().HaveCount(6);
                blocks.Take(5).Should().OnlyContain(x => x.HasBlock);
                blocks[5].Status.Should().Be(GzipSplittingStatus.StreamIsEnd);
                var actualBlocksStrings = blocks.Take(5).Select(x => x.Block.Decompress().AsString()).ToArray();
                actualBlocksStrings.Should().BeEquivalentTo(expectedBlockStrings);
            }

            private int SetLengthToHeader(byte[] bytes, BrokenLengthCases theCase)
            {
                switch (theCase)
                {
                    case BrokenLengthCases.WithoutLength:
                        SetLengthToHeader(bytes, 0);
                        return GzipHeader.Length;
                    case BrokenLengthCases.LessThanExpected:
                        SetLengthToHeader(bytes, bytes.Length/2);
                        return bytes.Length/2 + GzipHeader.Length;
                    case BrokenLengthCases.GreaterThanExpected:
                        SetLengthToHeader(bytes, bytes.Length + 1);
                        return bytes.Length + 1 + GzipHeader.Length;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(theCase), theCase, null);
                }
            }

            private static void SetLengthToHeader(byte[] bytes, int length)
            {
                // ReSharper disable once PossibleInvalidOperationException
                GzipHeader.FindFirst(bytes).Value.SetMimetypeBytes(BitConverter.GetBytes(length));
            }
        }
    }
}