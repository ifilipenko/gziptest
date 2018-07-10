using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using FluentAssertions;
using Parallel.Compression.Compression;
using Parallel.Compression.GzipFormat;
using Parallel.Compression.Models;
using Parallel.Compression.Tests.Helpers;
using Xunit;

namespace Parallel.Compression.Tests
{
    public class GzipBlockCompressionSpec
    {
        [SuppressMessage("ReSharper", "PossibleInvalidOperationException")]
        public class Compress
        {
            private readonly GzipBlockCompression gzipBlockCompression;

            public Compress()
            {
                gzipBlockCompression = new GzipBlockCompression();
            }

            [Fact]
            public void Should_compress_given_block_of_bytes()
            {
                var originalString = TestData.InputFileContent;
                var originalBytes = originalString.ToArraySegment();
                var block = new Block(originalBytes, 100);

                var compressedBlock = gzipBlockCompression.Compress(block, DecompressionHelpMode.NoDirtyHacks);

                compressedBlock.Bytes.Should().NotBeEmpty();
                compressedBlock.Bytes.Should().NotBeEquivalentTo(block.Bytes);
                compressedBlock.Bytes.Count.Should().BeLessThan(originalBytes.Count);
                compressedBlock.Bytes.DecompressToString().Should().Be(originalString);
            }

            [Fact]
            public void Should_return_empty_block_when_given_empty_block()
            {
                var block = new Block(new byte[0], 100);

                var compressedBlock = gzipBlockCompression.Compress(block, DecompressionHelpMode.NoDirtyHacks);

                compressedBlock.Bytes.ToArray().Should().BeEquivalentTo(new byte[0]);
            }

            [Fact]
            public void Should_return_block_with_offset_of_given_empty_block()
            {
                var block = new Block(new byte[0], 150);

                var compressedBlock = gzipBlockCompression.Compress(block, DecompressionHelpMode.NoDirtyHacks);

                compressedBlock.Offset.Should().Be(150);
            }

            [Fact]
            public void Should_return_block_with_offset_of_given_non_empty_block()
            {
                var block = new Block(TestData.InputFileContent.ToArraySegment(), 150);

                var compressedBlock = gzipBlockCompression.Compress(block, DecompressionHelpMode.NoDirtyHacks);

                compressedBlock.Offset.Should().Be(150);
            }

            [Fact]
            public void Should_compress_each_different_blocks_in_way_that_allow_to_decomress_it_like_one_big_block()
            {
                var expectedString = string.Join("", Enumerable.Range(1, 5).Select(x => x + TestData.ShortFileContent));
                var blocks = Enumerable.Range(1, 5)
                    .Select(x => new Block((x + TestData.ShortFileContent).ToArraySegment(), x))
                    .ToArray();

                var compressedBlocks = blocks.Select(x => gzipBlockCompression.Compress(x, DecompressionHelpMode.NoDirtyHacks)).ToArray();

                var bytes = ConcatBytesOfBlocks(compressedBlocks);
                bytes.DecompressToString().Should().Be(expectedString);
            }

            [Fact]
            public void Should_not_set_length_into_mime_type_section_when_enabled_clean_compression_mode()
            {
                var originalString = TestData.InputFileContent;
                var originalBytes = originalString.ToArraySegment();
                var block = new Block(originalBytes, 100);

                var compressedBlock = gzipBlockCompression.Compress(block, DecompressionHelpMode.NoDirtyHacks);

                var mimetype = GzipHeader.FindFirst(compressedBlock.Bytes).Value.MimetypeBytes.ToArray();
                BitConverter.ToInt32(mimetype).Should().Be(0);
            }

            [Fact]
            public void Should_set_length_into_mime_type_section_when_enabled_set_length_to_mimetype()
            {
                var originalString = TestData.InputFileContent;
                var originalBytes = originalString.ToArraySegment();
                var block = new Block(originalBytes, 100);

                var compressedBlock = gzipBlockCompression.Compress(block, DecompressionHelpMode.BlockLengthInMimetypeSection);

                var mimetype = GzipHeader.FindFirst(compressedBlock.Bytes).Value.MimetypeBytes.ToArray();
                BitConverter.ToInt32(mimetype).Should().Be(compressedBlock.Bytes.Count);
            }
            
            [Fact]
            public void Should_dont_corrupt_compressed_bytes_when_enabled_set_length_to_mimetype()
            {
                var originalString = TestData.InputFileContent;
                var originalBytes = originalString.ToArraySegment();
                var block = new Block(originalBytes, 100);

                var compressedBlock = gzipBlockCompression.Compress(block, DecompressionHelpMode.BlockLengthInMimetypeSection);

                compressedBlock.Bytes.DecompressToString().Should().Be(originalString);
            }

            private static byte[] ConcatBytesOfBlocks(Block[] blocks)
            {
                return blocks.SelectMany(b => b.Bytes).ToArray();
            }
        }

        public class Decompress
        {
            private readonly GzipBlockCompression gzipBlockCompression;

            public Decompress()
            {
                gzipBlockCompression = new GzipBlockCompression();
            }

            [Fact]
            public void Should_decompress_given_block_of_bytes()
            {
                var originalString = TestData.InputFileContent;
                var originalBytes = originalString.ToArraySegment();
                var compressedBlock = gzipBlockCompression.Compress(new Block(originalBytes, 100), DecompressionHelpMode.NoDirtyHacks);

                var decompressedBlock = gzipBlockCompression.Decompress(compressedBlock);

                decompressedBlock.Offset.Should().Be(100);
                decompressedBlock.Bytes.Should().BeEquivalentTo(originalBytes);
            }

            [Fact]
            public void Should_return_empty_block_when_given_empty_block()
            {
                var block = new Block(new byte[0], 100);

                var decompressedBlock = gzipBlockCompression.Decompress(block);

                decompressedBlock.Bytes.ToArray().Should().BeEquivalentTo(new byte[0]);
            }

            [Fact]
            public void Should_return_block_with_offset_of_given_empty_block()
            {
                var block = new Block(new byte[0], 150);

                var decompressedBlock = gzipBlockCompression.Decompress(block);

                decompressedBlock.Offset.Should().Be(150);
            }

            [Fact]
            public void Should_return_block_with_offset_of_given_non_empty_block()
            {
                var block = new Block(TestData.InputFileContent.GzipCompress(), 150);

                var decompressedBlock = gzipBlockCompression.Decompress(block);

                decompressedBlock.Offset.Should().Be(150);
            }
        }
    }
}