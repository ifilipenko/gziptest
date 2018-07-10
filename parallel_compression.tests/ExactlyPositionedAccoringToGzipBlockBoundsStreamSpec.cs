using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using FluentAssertions;
using Parallel.Compression.Decompression;
using Parallel.Compression.Decompression.Streams;
using Parallel.Compression.Helpers;
using Parallel.Compression.Tests.Helpers;
using Xunit;

namespace Parallel.Compression.Tests
{
    public class ExactlyPositionedAccoringToGzipBlockBoundsStreamSpec
    {
        [SuppressMessage("ReSharper", "ObjectCreationAsStatement")]
        public class Ctor
        {
            [Fact]
            public void Should_fail_when_stream_is_null()
            {
                Action action = () => new ExactlyPositionedAccoringToGzipBlockBoundsStream(null);

                action.Should().Throw<ArgumentNullException>();
            }
        }

        public class Read
        {
            private readonly Random random = new Random();

            [Fact]
            public void Should_read_to_the_end_if_gzip_have_only_one_gzip_block_and_buffer_is_bigger_than_stream()
            {
                var content = TestData.ShortFileContent;
                var inputStream = content.GzipCompress().AsStream();
                var buffer = new byte[inputStream.Length*3];

                var stream = new ExactlyPositionedAccoringToGzipBlockBoundsStream(new RewindableReadonlyStream(inputStream));

                var read = stream.Read(buffer);

                read.Should().Be((int) inputStream.Length);
                buffer.Segment(0, read).ToArray().Should().BeEquivalentTo(inputStream.ToArray());
            }

            [Fact]
            public void Should_read_to_the_end_if_gzip_have_only_one_gzip_block_and_buffer_is_smaller_than_stream()
            {
                var content = TestData.ShortFileContent;
                var inputStream = content.GzipCompress().AsStream();
                var buffer1 = new byte[inputStream.Length/2];
                var buffer2 = new byte[inputStream.Length/2];
                var buffer3 = new byte[inputStream.Length/2];

                var stream = new ExactlyPositionedAccoringToGzipBlockBoundsStream(new RewindableReadonlyStream(inputStream));

                var read1 = stream.Read(buffer1);
                var read2 = stream.Read(buffer2);
                var read3 = stream.Read(buffer3);

                read1.Should().Be((int) inputStream.Length/2);
                read2.Should().Be((int) inputStream.Length/2);
                read3.Should().Be(0);
                var totalBuffer = new[] {buffer1, buffer2}.JoinAll();
                totalBuffer.Should().BeEquivalentTo(inputStream.ToArray());
            }

            [Fact]
            public void Should_read_exactly_first_gzip_block_when_buffer_is_bigger_than_block()
            {
                var content = TestData.ShortFileContent;
                var expectedBuffer = content.GzipCompress();
                var blockContents = Enumerable.Range(0, 5)
                    .Select(x => content)
                    .ToList();
                var inputStream = blockContents.SelectMany(x => x.GzipCompress()).ToArray().AsStream();
                var buffer = new byte[inputStream.Length - 5];

                var stream = new ExactlyPositionedAccoringToGzipBlockBoundsStream(new RewindableReadonlyStream(inputStream));

                var read = stream.Read(buffer, 0, buffer.Length);

                read.Should().Be(expectedBuffer.Count);
                buffer.Segment(0, read).Should().BeEquivalentTo(expectedBuffer);
            }

            [Fact]
            public void Should_repeatadly_read_by_gzip_blocks_when_buffer_is_greater_than_many_blocks()
            {
                var content = TestData.ShortFileContent;
                var compressedContent = content.GzipCompress();
                var expectedBuffer = Enumerable.Range(0, 4)
                    .SelectMany(_ => compressedContent)
                    .ToList();
                var blockContents = Enumerable.Range(0, 5)
                    .Select(x => content)
                    .ToList();
                var inputStream = blockContents.SelectMany(x => x.GzipCompress()).ToArray().AsStream();
                var buffer = new byte[compressedContent.Count*4];
                var lastBlockOffset = compressedContent.Count*4;

                var stream = new ExactlyPositionedAccoringToGzipBlockBoundsStream(new RewindableReadonlyStream(inputStream));

                var offset = stream.Read(buffer, 0, buffer.Length);
                offset += stream.Read(buffer, offset, buffer.Length - offset);
                offset += stream.Read(buffer, offset, buffer.Length - offset);
                offset += stream.Read(buffer, offset, buffer.Length - offset);

                offset.Should().Be(lastBlockOffset);
                buffer.Should().BeEquivalentTo(expectedBuffer);
            }

            [Fact]
            public void Should_repeatadly_read_by_gzip_blocks_when_buffer_is_not_multiple_of_blocks_length()
            {
                var content = TestData.ShortFileContent;
                var compressedContent = content.GzipCompress();

                var block1 = compressedContent.ToArray();
                var block2 = compressedContent.Concat(RandomBytes(3)).ToArray();
                var block3 = compressedContent.ToArray();
                var block4 = compressedContent.Concat(RandomBytes(3)).ToArray();

                var inputStream = new[] {block1, block2, block3, block4}.SelectMany(x => x).ToArray().AsStream();
                var buffer = new byte[compressedContent.Count + 1];

                var stream = new ExactlyPositionedAccoringToGzipBlockBoundsStream(new RewindableReadonlyStream(inputStream));

                var readedBlock1 = ReadToRichLimit(stream, buffer, block1.Length);
                var readedBlock2 = ReadToRichLimit(stream, buffer, block2.Length);
                var readedBlock3 = ReadToRichLimit(stream, buffer, block3.Length);
                var readedBlock4 = ReadToRichLimit(stream, buffer, block4.Length);

                readedBlock1.Should().BeEquivalentTo(block1);
                readedBlock2.Should().BeEquivalentTo(block2);
                readedBlock3.Should().BeEquivalentTo(block3);
                readedBlock4.Should().BeEquivalentTo(block4);
            }

            [Fact]
            public void Should_read_gzip_blocks_when_some_of_them_are_bigger_than_buffer()
            {
                var content = TestData.ShortFileContent;
                var smallerThanBuffer = content.GzipCompress();
                var biggerThanBuffer = new string(
                        Enumerable.Range(0, 5)
                            .SelectMany(_ => TestData.InputFileContent.Shuffle())
                            .ToArray())
                    .GzipCompress();
                var inputStream = Enumerable.Repeat(smallerThanBuffer, 1)
                    .Append(biggerThanBuffer)
                    .Append(smallerThanBuffer)
                    .JoinIntoByteArray()
                    .AsStream();
                var buffer = new byte[(int) (biggerThanBuffer.Count*1.5)];

                var stream = new ExactlyPositionedAccoringToGzipBlockBoundsStream(new RewindableReadonlyStream(inputStream));

                var firstBlock = ReadAsSmallBlock();
                var bigBlock = ReadAsBigBlock(biggerThanBuffer.Count);
                var lastBlock = ReadAsSmallBlock();

                firstBlock.Should().BeEquivalentTo(smallerThanBuffer);
                bigBlock.Should().BeEquivalentTo(biggerThanBuffer);
                lastBlock.Should().BeEquivalentTo(smallerThanBuffer);

                byte[] ReadAsSmallBlock()
                {
                    var read = stream.Read(buffer, 0, buffer.Length);
                    return buffer.Take(read).ToArray();
                }

                byte[] ReadAsBigBlock(int count)
                {
                    var bytes = new List<byte>(count);
                    while (bytes.Count < count)
                    {
                        var read = stream.Read(buffer, 0, buffer.Length);
                        if (read == 0)
                            break;
                        bytes.AddRange(buffer.Segment(0, read));
                    }

                    return bytes.ToArray();
                }
            }

            private byte[] ReadToRichLimit(Stream stream, byte[] buffer, int limit)
            {
                var bytes = new List<byte>(limit);
                while (bytes.Count < limit)
                {
                    var read = stream.Read(buffer, 0, buffer.Length);
                    if (read == 0)
                        break;
                    bytes.AddRange(buffer.Segment(0, read));
                }

                return bytes.ToArray();
            }

            private IEnumerable<byte> RandomBytes(int count)
            {
                return Enumerable.Range(0, count).Select(_ => (byte) random.Next(1, byte.MaxValue));
            }
        }

        public class Position
        {
            [Fact]
            public void Should_be_equal_to_position_of_given_stream()
            {
                var inputStream = TestData.ShortFileContent.GzipCompress().AsStream();
                inputStream.Position = inputStream.Length/2;

                var stream = new ExactlyPositionedAccoringToGzipBlockBoundsStream(new RewindableReadonlyStream(inputStream));

                stream.Position.Should().Be(inputStream.Position);
            }

            [Fact]
            public void Should_changed_according_to_readed_bytes()
            {
                var gzipCompress = TestData.ShortFileContent.GzipCompress();
                var contents = Enumerable.Range(0, 3).Select(_ => gzipCompress).ToArray();
                var inputStream = contents.JoinIntoByteArray().AsStream();
                var buffer = new byte[inputStream.Length];
                inputStream.Position = gzipCompress.Count;

                var stream = new ExactlyPositionedAccoringToGzipBlockBoundsStream(new RewindableReadonlyStream(inputStream));

                stream.Read(buffer);

                stream.Position.Should().Be(2*gzipCompress.Count);
            }
        }

        public class IsEndOfStream
        {
            [Fact]
            public void Should_be_false_when_given_empty_stream_and_no_attempts_to_read_it()
            {
                var stream = new ExactlyPositionedAccoringToGzipBlockBoundsStream(new RewindableReadonlyStream(new MemoryStream()));

                stream.IsEndOfStream.Should().BeFalse();
            }

            [Fact]
            public void Should_indicate_that_empty_stream_is_end_after_read_attempt()
            {
                var stream = new ExactlyPositionedAccoringToGzipBlockBoundsStream(new RewindableReadonlyStream(new MemoryStream()));

                stream.Read(new byte[100]);

                stream.IsEndOfStream.Should().BeTrue();
            }

            [Fact]
            public void Should_be_false_while_gzip_buffer_is_not_end()
            {
                var gzipCompress = TestData.ShortFileContent.GzipCompress();
                var contents = Enumerable.Range(0, 3).Select(_ => gzipCompress).ToArray();
                var inputStream = contents.JoinIntoByteArray().AsStream();
                var buffer = new byte[inputStream.Length + 1];

                var stream = new ExactlyPositionedAccoringToGzipBlockBoundsStream(new RewindableReadonlyStream(inputStream));

                var read1 = stream.Read(buffer);
                inputStream.Position.Should().Be(stream.Length);
                stream.Position.Should().Be(read1);
                stream.IsEndOfStream.Should().BeFalse();

                var read2 = stream.Read(buffer);
                stream.Position.Should().Be(read1 + read2);
                stream.IsEndOfStream.Should().BeFalse();

                stream.Read(buffer);
                stream.Position.Should().Be(inputStream.Length);
                stream.IsEndOfStream.Should().BeTrue();
            }
        }
    }
}