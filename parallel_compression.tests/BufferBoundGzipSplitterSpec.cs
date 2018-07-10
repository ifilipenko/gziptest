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
    public class BufferBoundGzipSplitterSpec
    {
        [SuppressMessage("ReSharper", "ObjectCreationAsStatement")]
        [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
        public class Ctor
        {
            [Theory]
            [InlineData(-1)]
            [InlineData(0)]
            [InlineData(10)]
            public void Should_throw_when_given_invalid_buffer_size(int bufferSize)
            {
                Action action = () => new BufferBoundGzipSplitter(bufferSize, Substitute.For<ILog>());

                action.Should().Throw<ArgumentOutOfRangeException>();
            }

            [Fact]
            public void Should_fail_when_given_null_logger()
            {
                Action action = () => new BufferBoundGzipSplitter(128, null);

                action.Should().Throw<ArgumentNullException>();
            }
        }

        public class SplitToIndependentBlocks
        {
            private readonly ILog log;

            public SplitToIndependentBlocks(ITestOutputHelper output)
            {
                log = new TestLog(output);
            }
            
            [Fact]
            public void Should_return_nothing_when_stream_had_read_to_the_end()
            {
                var compressed = TestData.ShortFileContent.GzipCompress();
                var stream = compressed.ToArray().AsStream();
                stream.Position = stream.Length;
                var splitter = new BufferBoundGzipSplitter(compressed.Count, log);
                var rewindableStream = new RewindableReadonlyStream(stream);

                var blocks = splitter.SplitToIndependentBlocks(rewindableStream).ToArray();

                blocks.Should().HaveCount(1);
                blocks[0].Status.Should().Be(GzipSplittingStatus.StreamIsEnd);
                rewindableStream.ReturnedBuffer.Should().HaveCount(0);
            }
            
            [Fact]
            public void Should_return_block_when_is_is_equal_to_buffer_size_and_single_in_stream()
            {
                var compressed = TestData.ShortFileContent.GzipCompress();
                var stream = compressed.ToArray().AsStream();
                var splitter = new BufferBoundGzipSplitter(compressed.Count, log);
                var rewindableStream = new RewindableReadonlyStream(stream);

                var blocks = splitter.SplitToIndependentBlocks(rewindableStream).ToArray();

                blocks.Should().HaveCount(2);
                blocks[0].HasBlock.Should().BeTrue();
                blocks[1].Status.Should().Be(GzipSplittingStatus.StreamIsEnd);
                rewindableStream.ReturnedBuffer.Should().HaveCount(0);
            }
            
            [Fact]
            public void Should_return_block_when_it_is_smaller_than_buffer_size_and_single_in_stream()
            {
                var compressed = TestData.ShortFileContent.GzipCompress();
                var stream = compressed.ToArray().AsStream();
                var splitter = new BufferBoundGzipSplitter(compressed.Count + 1, log);
                var rewindableStream = new RewindableReadonlyStream(stream);

                var blocks = splitter.SplitToIndependentBlocks(rewindableStream).ToArray();

                blocks.Should().HaveCount(2);
                blocks[0].HasBlock.Should().BeTrue();
                blocks[1].Status.Should().Be(GzipSplittingStatus.StreamIsEnd);
                rewindableStream.ReturnedBuffer.Should().HaveCount(0);
            }

            [Fact]
            public void Should_not_return_any_blocks_when_block_is_greater_than_buffer_size()
            {
                var compressed = TestData.ShortFileContent.GzipCompress();
                var stream = compressed.ToArray().AsStream();
                var bufferSize = compressed.Count - 1;
                var splitter = new BufferBoundGzipSplitter(bufferSize, log);
                var rewindableStream = new RewindableReadonlyStream(stream);

                var blocks = splitter.SplitToIndependentBlocks(rewindableStream).ToArray();

                blocks.Should().HaveCount(1);
                blocks[0].Status.Should().Be(GzipSplittingStatus.CantReadBlock);
                rewindableStream.ReturnedBuffer.Should().HaveCount(bufferSize);
            }

            [Fact]
            public void Should_return_all_compressed_blocks_when_buffer_is_greater_than_whole_stream()
            {
                var stream = Enumerable.Range(0, 5).SelectMany(_ =>  TestData.ShortFileContent.Shuffle().GzipCompress()).ToArray().AsStream();
                var splitter = new BufferBoundGzipSplitter((int) stream.Length, log);
                var rewindableStream = new RewindableReadonlyStream(stream);

                var blocks = splitter.SplitToIndependentBlocks(rewindableStream).ToArray();

                blocks.Should().HaveCount(6);
                blocks.Take(5).Should().OnlyContain(x => x.HasBlock);
                blocks[5].Status.Should().Be(GzipSplittingStatus.StreamIsEnd);
                rewindableStream.ReturnedBuffer.Should().HaveCount(0);
            }

            [Fact]
            public void Should_return_all_compressed_blocks_when_buffer_is_greater_than_all_blocks_in_the_stream()
            {
                var blocks = Enumerable.Range(0, 5).Select(_ => TestData.ShortFileContent.Shuffle().GzipCompress()).ToArray();
                var stream = blocks.SelectMany(x => x).ToArray().AsStream();
                var splitter = new BufferBoundGzipSplitter(blocks.Max(x => x.Count) + GzipHeader.Length, log);
                var rewindableStream = new RewindableReadonlyStream(stream);
                
                var actualBlocks = splitter.SplitToIndependentBlocks(rewindableStream).ToArray();

                actualBlocks.Should().HaveCount(6);
                actualBlocks.Take(5).Should().OnlyContain(x => x.HasBlock);
                actualBlocks[5].Status.Should().Be(GzipSplittingStatus.StreamIsEnd);
                rewindableStream.ReturnedBuffer.Should().HaveCount(0);
            }
            
            [Fact]
            public void Should_not_return_any_blocks_when_buffer_is_equal_to_block_size_and_stream_contains_many_blocks()
            {
                var compressed = TestData.ShortFileContent.GzipCompress();
                var stream = Enumerable.Range(0, 5).SelectMany(_ => compressed).ToArray().AsStream();
                var bufferSize = compressed.Count;
                var splitter = new BufferBoundGzipSplitter(bufferSize, log);
                var rewindableStream = new RewindableReadonlyStream(stream);
                
                var blocks = splitter.SplitToIndependentBlocks(rewindableStream).ToArray();

                blocks.Should().HaveCount(1);
                blocks[0].Status.Should().Be(GzipSplittingStatus.CantReadBlock);
                rewindableStream.ReturnedBuffer.Should().HaveCount(bufferSize);
            }
            
            [Fact]
            public void Should_not_return_any_blocks_when_buffer_is_not_enoght_to_locate_next_header_and_stream_contains_many_blocks()
            {
                var compressed = TestData.ShortFileContent.GzipCompress();
                var stream = Enumerable.Range(0, 5).SelectMany(_ => compressed).ToArray().AsStream();
                var bufferSize = compressed.Count + GzipHeader.Length - 1;
                var splitter = new BufferBoundGzipSplitter(bufferSize, log);
                var rewindableStream = new RewindableReadonlyStream(stream);
                
                var blocks = splitter.SplitToIndependentBlocks(rewindableStream).ToArray();

                blocks.Should().HaveCount(1);
                blocks[0].Status.Should().Be(GzipSplittingStatus.CantReadBlock);
                rewindableStream.ReturnedBuffer.Should().HaveCount(bufferSize);
            }

            [Fact]
            public void Should_return_error_when_stream_is_not_located_to_gzip_header()
            {
                var compressed = TestData.ShortFileContent.GzipCompress();
                var stream = Enumerable.Range(0, 5).SelectMany(_ => compressed).ToArray().AsStream();
                var bufferSize = compressed.Count + GzipHeader.Length + 1;
                var splitter = new BufferBoundGzipSplitter(bufferSize, log);
                stream.Position = 3;
                var rewindableStream = new RewindableReadonlyStream(stream);
                
                var blocks = splitter.SplitToIndependentBlocks(rewindableStream).ToArray();
               
                blocks.Should().HaveCount(1);
                blocks[0].Status.Should().Be(GzipSplittingStatus.WrongFormat);
                rewindableStream.ReturnedBuffer.Should().HaveCount(bufferSize);
            }
            
            [Fact]
            public void Should_read_stream_while_enumeration_blocks_execute()
            {
                var blocks = Enumerable.Range(0, 5).Select(_ => TestData.ShortFileContent.Shuffle().GzipCompress()).ToArray();
                var stream = blocks.SelectMany(x => x).ToArray().AsStream();
                var splitter = new BufferBoundGzipSplitter(blocks.Max(x => x.Count) + GzipHeader.Length, log);
                
                var positions = splitter.SplitToIndependentBlocks(new RewindableReadonlyStream(stream)).Select(_ => stream.Position).ToList();

                positions.Should().HaveCount(6);
                positions.Take(5).Should().OnlyHaveUniqueItems().And.BeInAscendingOrder();
                positions[4].Should().Be(positions[5]).And.Be(stream.Length);
            }
            
            [Fact]
            public void Should_split_into_correctly_decomressable_independent_blocks()
            {
                var sourceContents = Enumerable.Range(0, 5).Select(_ => TestData.ShortFileContent.Shuffle()).ToArray();
                var blocks = sourceContents.Select(x => x.GzipCompress()).ToArray();
                var stream = blocks.SelectMany(x => x).ToArray().AsStream();
                var splitter = new BufferBoundGzipSplitter(blocks.Max(x => x.Count) + GzipHeader.Length, log);

                var actualBlocks = splitter.SplitToIndependentBlocks(new RewindableReadonlyStream(stream)).ToArray();

                actualBlocks.Take(5).Select(x => x.Block.Decompress().AsString()).Should().BeEquivalentTo(sourceContents);
            }
        }
    }
}