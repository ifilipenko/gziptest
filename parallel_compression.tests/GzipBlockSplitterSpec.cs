using System.IO;
using System.Linq;
using FluentAssertions;
using Parallel.Compression.Compression;
using Parallel.Compression.Decompression.GzipSplitting;
using Parallel.Compression.Helpers;
using Parallel.Compression.Logging;
using Parallel.Compression.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Parallel.Compression.Tests
{
    public class GzipBlockSplitterSpec
    {
        public class SplitBlocks
        {
            private readonly ILog log;

            public SplitBlocks(ITestOutputHelper output)
            {
                log = new TestLog(output);
            }

            [Fact]
            public void Should_split_to_blocks_when_compressed_with_length_in_headers()
            {
                var contents = Enumerable.Range(0, 5).Select(x => x + " " + TestData.ShortFileContent.Shuffle()).ToArray();
                var stream = contents.SelectMany(x =>
                        x.CompessWithDecompressionHelp(DecompressionHelpMode.BlockLengthInMimetypeSection))
                    .ToArray().AsStream();
                var gzipBlockSplitter = new GzipBlockSplitter(1.Kilobytes(), 1.Kilobytes(), log);

                var splitBlocks = gzipBlockSplitter.SplitBlocks(stream).ToArray();

                splitBlocks.Should().HaveCount(5).And.AllBeAssignableTo<IndependentGzipBlock>();
                var decompressedBlocks = splitBlocks.OfType<IndependentGzipBlock>().Select(x => x.Decompress().AsString()).ToArray();
                decompressedBlocks.Should().BeEquivalentTo(contents);
            }
            
            [Fact]
            public void Should_split_to_blocks_when_compressed_with_length_in_headers_but_blocks_is_greater_than_limit()
            {
                var contents = Enumerable.Range(0, 5).Select(x => x + " " + TestData.ShortFileContent.Shuffle()).ToArray();
                var stream = contents.SelectMany(x =>
                        x.CompessWithDecompressionHelp(DecompressionHelpMode.BlockLengthInMimetypeSection))
                    .ToArray().AsStream();
                var gzipBlockSplitter = new GzipBlockSplitter(11, 1.Kilobytes(), log);

                var splitBlocks = gzipBlockSplitter.SplitBlocks(stream).ToArray();

                splitBlocks.Should().HaveCount(5).And.AllBeAssignableTo<IndependentGzipBlock>();
                var decompressedBlocks = splitBlocks.OfType<IndependentGzipBlock>().Select(x => x.Decompress().AsString()).ToArray();
                decompressedBlocks.Should().BeEquivalentTo(contents);
            }
            
            [Fact]
            public void Should_split_to_blocks_when_compressed_without_length_in_header()
            {
                var contents = Enumerable.Range(0, 5).Select(x => x + " " + TestData.ShortFileContent.Shuffle()).ToArray();
                var stream = contents.SelectMany(x =>
                        x.CompessWithDecompressionHelp(DecompressionHelpMode.NoDirtyHacks))
                    .ToArray().AsStream();
                var gzipBlockSplitter = new GzipBlockSplitter(1.Kilobytes(), 1.Kilobytes(), log);

                var splitBlocks = gzipBlockSplitter.SplitBlocks(stream).ToArray();

                splitBlocks.Should().HaveCount(5).And.AllBeAssignableTo<IndependentGzipBlock>();
                var decompressedBlocks = splitBlocks.OfType<IndependentGzipBlock>().Select(x => x.Decompress().AsString()).ToArray();
                decompressedBlocks.Should().BeEquivalentTo(contents);
            }
            
            [Fact]
            public void Should_split_to_blocks_when_compressed_with_length_but_blocks_is_greater_than_limits()
            {
                var contents = Enumerable.Range(0, 5).Select(x => x + " " + TestData.ShortFileContent.Shuffle()).ToArray();
                var expectedText = contents.JoinStrings(); 
                var stream = contents.SelectMany(x =>
                        x.CompessWithDecompressionHelp(DecompressionHelpMode.NoDirtyHacks))
                    .ToArray().AsStream();
                var gzipBlockSplitter = new GzipBlockSplitter(11, 11, log);

                var splitBlocks = gzipBlockSplitter.SplitBlocks(stream).ToArray();

                splitBlocks.Should().HaveCount(1).And.AllBeAssignableTo<StreamingGzipBlock>();
                
                var outputStream = new MemoryStream();
                splitBlocks.First().As<StreamingGzipBlock>().WriteDecompressedDataTo(outputStream);
                outputStream.ToArray().AsString().Should().Be(expectedText);
            }
        }
    }
}