using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using FluentAssertions;
using Parallel.Compression.Compression;
using Parallel.Compression.Decompression;
using Parallel.Compression.Decompression.GzipSplitting;
using Parallel.Compression.Decompression.Streams;
using Parallel.Compression.GzipFormat;
using Parallel.Compression.Helpers;
using Parallel.Compression.Tests.Helpers;
using Xunit;

namespace Parallel.Compression.Tests
{
    public class StreamingGzipBlockSpec
    {
        [SuppressMessage("ReSharper", "ObjectCreationAsStatement")]
        [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
        public class Ctor
        {
            [Fact]
            public void Should_throw_when_given_null_stream()
            {
                Action action = () => new StreamingGzipBlock(null);

                action.Should().Throw<ArgumentNullException>();
            }
        }

        public class WriteDecompressedDataTo
        {
            [Fact]
            public void Should_decompress_stream_with_only_one_block_and_write_the_data_into_output_stream()
            {
                var stream = TestData.ShortFileContent.GzipCompress().AsStream();
                var gzipBlock = new StreamingGzipBlock(new RewindableReadonlyStream(stream));
                var outputStream = new MemoryStream();

                gzipBlock.WriteDecompressedDataTo(outputStream);

                outputStream.ToArray().AsString().Should().Be(TestData.ShortFileContent);
            }

            [Fact]
            public void Should_decompress_stream_with_many_blocks_and_write_the_data_into_output_stream()
            {
                var content = TestData.ShortFileContent;
                var blockContents = Enumerable.Range(0, 5)
                    .Select(x => content.Shuffle().Remove(content.Length - x, x))
                    .ToList();
                var stream = blockContents.SelectMany(x => x.GzipCompress()).ToArray().AsStream();
                var expectedContent = string.Join("", blockContents);

                var outputStream = new MemoryStream();
                var gzipBlock = new StreamingGzipBlock(new RewindableReadonlyStream(stream));

                gzipBlock.WriteDecompressedDataTo(outputStream);

                outputStream.ToArray().AsString().Should().Be(expectedContent);
            }

            [Fact]
            public void Should_decompress_multysection_gzip_with_length_in_metadata()
            {
                var expectedString = Enumerable.Range(0, 6).Select(_ => TestData.ShortFileContent).JoinStrings();
                var compressed = TestData.ShortFileContent
                    .CompessWithDecompressionHelp(DecompressionHelpMode.BlockLengthInMimetypeSection);
                var stream = Enumerable.Range(0, 6)
                    .Select(_ => compressed)
                    .SelectMany(x => x)
                    .ToArray()
                    .AsStream();

                var outputStream = new MemoryStream();
                var gzipBlock = new StreamingGzipBlock(new RewindableReadonlyStream(stream));

                gzipBlock.WriteDecompressedDataTo(outputStream);

                outputStream.ToArray().AsString().Should().Be(expectedString);
            }

            [Fact]
            public void Should_decompress_gzip_from_given_offset()
            {
                var content = TestData.ShortFileContent;
                var blockContents = Enumerable.Range(0, 5)
                    .Select(x => content.Remove(content.Length - x, x))
                    .Append(TestData.InputFileContent + TestData.InputFileContent)
                    .Append(content)
                    .ToList();
                var expectedContent = string.Join(string.Empty, blockContents.Skip(5));

                var compressedBlocks = blockContents.Select(x => x.GzipCompress()).ToArray();
                var stream = compressedBlocks.SelectMany(x => x).ToArray().AsStream();
                stream.Position = compressedBlocks.Take(5).Sum(x => x.Count);

                var outputStream = new MemoryStream();
                var gzipBlock = new StreamingGzipBlock(new RewindableReadonlyStream(stream));

                gzipBlock.WriteDecompressedDataTo(outputStream);

                outputStream.ToArray().AsString().Should().Be(expectedContent);
            }

            [Fact]
            public void Should_decompress_gzip_from_stream_with_returned_buffer()
            {
                var contents = new []
                {
                    TestData.ShortFileContent, 
                    TestData.InputFileContent, 
                    TestData.InputFileContent, 
                    TestData.ShortFileContent
                };
                var compressedContents = contents.Select(x => x.GzipCompress()).ToArray();
                var expectedContent = contents.JoinStrings();

                var stream = compressedContents.JoinIntoByteArray().AsStream();
                var inputStream = new RewindableReadonlyStream(stream);
                var firstBlockSize = compressedContents.First().Count;
                ReadBufferAndReturnItToStreamBack(inputStream, firstBlockSize + GzipHeader.Length + 1);

                var outputStream = new MemoryStream();
                var gzipBlock = new StreamingGzipBlock(inputStream);

                gzipBlock.WriteDecompressedDataTo(outputStream);

                outputStream.ToArray().AsString().Should().Be(expectedContent);
            }

            private static void ReadBufferAndReturnItToStreamBack(RewindableReadonlyStream inputStream, int bufferSize)
            {
                var buffer = new byte[bufferSize];
                var read = inputStream.Read(buffer);
                read.Should().Be(buffer.Length);

                inputStream.ReturnTailOfReadedBytes(buffer);
            }
        }
    }
}