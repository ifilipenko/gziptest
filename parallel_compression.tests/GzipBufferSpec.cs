using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using FluentAssertions;
using Parallel.Compression.Compression;
using Parallel.Compression.Decompression;
using Parallel.Compression.GzipFormat;
using Parallel.Compression.Helpers;
using Parallel.Compression.Tests.Helpers;
using Xunit;

namespace Parallel.Compression.Tests
{
    public class GzipBufferSpec
    {
        private static readonly byte[] GzipHeaderBytes = TestData.ShortFileContent.GzipCompress().Take(GzipHeader.Length).ToArray();

        public class Default
        {
            private GzipBuffer defaultInstance = default;

            [Fact]
            public void Should_return_empty_bytes()
            {
                defaultInstance.Bytes.Array.Should().BeNull();
            }

            [Fact]
            public void Should_return_empty_headers()
            {
                defaultInstance.Headers.Should().HaveCount(0);
            }

            [Fact]
            public void Should_return_empty_end_header_part()
            {
                defaultInstance.PossiblePartOfHeaderAtTheEnd.Should().BeNull();
            }

            [Fact]
            public void Should_indicate_that_buffer_have_no_parts_and_headers()
            {
                defaultInstance.NoHeadersOrParts.Should().BeTrue();
            }

            [Fact]
            public void Should_return_default_when_try_to_copy_to_owning_buffer()
            {
                var ownedBuffer = defaultInstance.ToOwnedBuffer();

                ownedBuffer.Should().Be(defaultInstance);
            }

            [Fact]
            public void Should_return_defaults_when_try_to_extract_first_header()
            {
                var (firstBlock, afterHeader) = defaultInstance.CutFirstBlock();

                firstBlock.Array.Should().BeNull();
                afterHeader.Should().Be(defaultInstance);
            }
        }

        [SuppressMessage("ReSharper", "ObjectCreationAsStatement")]
        [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
        public class Ctor
        {
            public static readonly TheoryData<int> PartsOfHeaderAtTheEnd = Enumerable.Range(1, GzipHeader.Length - 1).ToTheoryData();

            [Fact]
            public void Should_fail_when_given_default_array_segment()
            {
                Action action = () => new GzipBuffer(default);

                action.Should().Throw<ArgumentException>();
            }

            [Fact]
            public void Should_create_empty_when_given_empty_bytes()
            {
                var buffer = new GzipBuffer(ArraySegment<byte>.Empty);

                buffer.Bytes.Should().BeEquivalentTo(ArraySegment<byte>.Empty);
                buffer.NoHeadersOrParts.Should().BeTrue();
            }

            [Fact]
            public void Should_initialize_with_given_bytes()
            {
                var arraySegment = TestData.ShortFileContent.ToArraySegment().ShiftOffsetRight(10);

                var buffer = new GzipBuffer(arraySegment);

                buffer.Bytes.Should().BeEquivalentTo(arraySegment);
            }

            [Fact]
            public void Should_set_no_headers_and_parts_if_given_bytes_does_not_contain_any_of_them()
            {
                var arraySegment = TestData.ShortFileContent.ToArraySegment();

                var buffer = new GzipBuffer(arraySegment);

                buffer.Headers.Should().BeEmpty();
                buffer.PossiblePartOfHeaderAtTheEnd.Should().BeNull();
                buffer.NoHeadersOrParts.Should().BeTrue();
            }

            [Fact]
            public void Should_found_all_gzip_headers()
            {
                var contents = Enumerable.Range(0, 5).Select(_ => TestData.ShortFileContent.Shuffle().GzipCompress()).ToArray();
                var expectedPositions = GetContentsOffsets(contents).ToArray();
                var arraySegment = contents.JoinIntoByteArray();

                var buffer = new GzipBuffer(arraySegment);

                buffer.Headers.Should().HaveSameCount(contents);
                buffer.Headers.Select(x => x.Position).Should().BeEquivalentTo(expectedPositions);
                buffer.NoHeadersOrParts.Should().BeFalse();
            }

            [Fact]
            public void Should_found_all_gzip_headers_when_bytes_is_not_start_with_header()
            {
                var contents = Enumerable.Range(0, 5).Select(_ => TestData.ShortFileContent.Shuffle().GzipCompress()).ToArray();
                var expectedPositions = GetContentsOffsets(contents).Skip(1).ToArray();
                var arraySegment = contents.JoinIntoByteArray().Segment(1);

                var buffer = new GzipBuffer(arraySegment);

                buffer.Headers.Should().HaveSameCount(expectedPositions);
                buffer.Headers.Select(x => x.Position).Should().BeEquivalentTo(expectedPositions);
            }

            [Theory]
            [MemberData(nameof(PartsOfHeaderAtTheEnd))]
            public void Should_found_end_possible_part_of_header_at_the_end_when_no_other_headers_found(int partOfHeader)
            {
                var anyFirstBytes = TestData.ShortFileContent.ToBytes();
                var part = GzipHeaderBytes.Take(partOfHeader).ToArray();
                var bytes = anyFirstBytes.Concat(part).ToArray();
                var expectedPosition = bytes.Length - partOfHeader;

                var buffer = new GzipBuffer(bytes);

                buffer.PossiblePartOfHeaderAtTheEnd.Should().Be(expectedPosition);
                buffer.NoHeadersOrParts.Should().BeFalse();
            }

            [Theory]
            [MemberData(nameof(PartsOfHeaderAtTheEnd))]
            public void Should_found_end_possible_part_of_header_at_the_end_when_other_headers_found(int partOfHeader)
            {
                var anyFirstBytes = Enumerable.Range(0, 5).SelectMany(_ => TestData.ShortFileContent.Shuffle().GzipCompress()).ToArray();
                var part = GzipHeaderBytes.Take(partOfHeader).ToArray();
                var bytes = anyFirstBytes.Concat(part).ToArray();
                var expectedPosition = bytes.Length - partOfHeader;

                var buffer = new GzipBuffer(bytes);

                buffer.PossiblePartOfHeaderAtTheEnd.Should().Be(expectedPosition);
                buffer.NoHeadersOrParts.Should().BeFalse();
            }
        }

        public class ToOwnedBuffer
        {
            private const int HeadersNotInSegment = 1;
            private const int SourceArrayOffset = 15;
            private GzipBuffer buffer;
            private readonly ArraySegment<byte> givenBytes;
            private readonly ArraySegment<byte>[] contents;
            private readonly int partOfHeaderInSourceArray;
            private readonly byte[] givenBytesArray;

            public ToOwnedBuffer()
            {
                contents = Enumerable.Range(0, 5)
                    .Select(_ => TestData.ShortFileContent.Shuffle().GzipCompress())
                    .ToArray();
                givenBytes = contents.SelectMany(x => x)
                    .Concat(GzipHeaderBytes.Take(5))
                    .ToArray()
                    .Segment(SourceArrayOffset);
                // ReSharper disable once ImpureMethodCallOnReadonlyValueField
                givenBytesArray = givenBytes.ToArray();
                partOfHeaderInSourceArray = givenBytes.Count + SourceArrayOffset - 5;

                buffer = new GzipBuffer(givenBytes);
            }

            [Fact]
            public void Should_copy_given_segment_bytes()
            {
                var ownedBuffer = buffer.ToOwnedBuffer();

                ownedBuffer.Bytes.Array.Should().NotBeSameAs(givenBytes);
                ownedBuffer.Bytes.ToArray().Should().BeEquivalentTo(givenBytesArray);
            }

            [Fact]
            public void Should_copy_headers_to_point_to_new_segment()
            {
                var expectedHeadersPositions = GetContentsOffsets(contents).Skip(HeadersNotInSegment).Select(x => x - SourceArrayOffset).ToArray();

                var ownedBuffer = buffer.ToOwnedBuffer();

                ownedBuffer.Headers.Select(x => x.Position).Should().BeEquivalentTo(expectedHeadersPositions);
                ownedBuffer.Headers.Select(x => x.Bytes.Array).Should().OnlyContain(x => ReferenceEquals(x, ownedBuffer.Bytes.Array));
                ownedBuffer.Headers.Select(x => x.Bytes).Should().OnlyContain(x => GzipHeader.IsHeader(x));
            }

            [Fact]
            public void Should_not_correct_position_of_possible_part_of_header_when_it_is_not_present_in_the_source_array()
            {
                givenBytes.Fill(partOfHeaderInSourceArray, (byte) 1);
                buffer = new GzipBuffer(givenBytes);

                var ownedBuffer = buffer.ToOwnedBuffer();

                ownedBuffer.PossiblePartOfHeaderAtTheEnd.Should().BeNull();
            }

            [Fact]
            public void Should_correct_position_of_possible_part_of_header()
            {
                var expectedPossiblePartOfHederOffset = partOfHeaderInSourceArray - SourceArrayOffset;

                var ownedBuffer = buffer.ToOwnedBuffer();

                ownedBuffer.PossiblePartOfHeaderAtTheEnd.Should().Be(expectedPossiblePartOfHederOffset);
            }
        }

        public class CutFirstBlock
        {
            private readonly List<ArraySegment<byte>> blocks;
            private readonly ArraySegment<byte> givenBytes;

            public CutFirstBlock()
            {
                var anyFirstBytes = TestData.ShortFileContent.ToBytes();
                var compressedContents = Enumerable.Range(0, 5)
                    .Select(_ => TestData.ShortFileContent.Shuffle().GzipCompress())
                    .ToArray();

                givenBytes = compressedContents.Prepend(anyFirstBytes).JoinIntoByteArray().Segment(anyFirstBytes.Length);

                var offset = anyFirstBytes.Length;
                blocks = new List<ArraySegment<byte>>();
                foreach (var compressedContent in compressedContents)
                {
                    var block = new ArraySegment<byte>(givenBytes.Array, offset, compressedContent.Count);
                    blocks.Add(block);
                    block.ToArray().Should().BeEquivalentTo(compressedContent);
                    offset += compressedContent.Count;
                }
            }

            [Fact]
            public void Should_cut_segment_before_second_header_if_buffer_starts_with_a_header()
            {
                var expectedHeadersPositions = blocks.Select(x => x.Offset).Skip(1).ToArray();
                var buffer = new GzipBuffer(givenBytes);

                var (firstBlock, leftBuffer) = buffer.CutFirstBlock();

                firstBlock.Should().BeEquivalentTo(blocks.First());
                leftBuffer.Headers.Select(x => x.Position).Should().BeEquivalentTo(expectedHeadersPositions);
            }

            [Fact]
            public void Should_cut_segment_before_first_header_if_buffer_have_no_header_at_start()
            {
                var expectedHeadersPositions = blocks.Select(x => x.Offset).Skip(1).ToArray();
                var shiftBytesCount = GzipHeaderBytes.Length + 5;
                var shftedBytes = givenBytes.ShiftOffsetRight(shiftBytesCount);
                var expectedSegment = blocks.First().ShiftOffsetRight(shiftBytesCount);
                
                var buffer = new GzipBuffer(shftedBytes);

                var (firstBlock, leftBuffer) = buffer.CutFirstBlock();

                firstBlock.Should().BeEquivalentTo(expectedSegment);
                leftBuffer.Headers.Select(x => x.Position).Should().BeEquivalentTo(expectedHeadersPositions);
            }

            [Fact]
            public void Should_return_block_and_left_part_of_buffer_with_the_same_bytes_array()
            {
                var buffer = new GzipBuffer(givenBytes);

                var (firstBlock, leftBuffer) = buffer.CutFirstBlock();

                firstBlock.Array.Should().BeSameAs(givenBytes.Array);
                leftBuffer.Bytes.Array.Should().BeSameAs(givenBytes.Array);
            }

            [Fact]
            public void Should_return_whole_bytes_segment_and_empty_left_buffer_when_have_no_any_gzip_headers_or_parts()
            {
                var noHeadersBytes = TestData.ShortFileContent.ToBytes();
                var buffer = new GzipBuffer(noHeadersBytes);

                var (firstBlock, leftBuffer) = buffer.CutFirstBlock();

                firstBlock.Should().BeEquivalentTo(noHeadersBytes);
                leftBuffer.Bytes.Count.Should().Be(0);
            }

            [Fact]
            public void Should_return_whole_bytes_segment_when_buffer_start_with_header_and_have_no_end_header_parts()
            {
                var oneBlockBytes = TestData.ShortFileContent.GzipCompress();
                var buffer = new GzipBuffer(oneBlockBytes);

                var (firstBlock, leftBuffer) = buffer.CutFirstBlock();

                firstBlock.Should().BeEquivalentTo(oneBlockBytes);
                leftBuffer.Bytes.Count.Should().Be(0);
            }

            [Fact]
            public void Should_return_bytes_segment_and_left_part_of_header_at_the_end_when_bytes_starts_with_header()
            {
                var compressed = TestData.ShortFileContent.GzipCompress();
                var someBytes = TestData.ShortFileContent.ToBytes();
                const int partOfHeader = 5;
                var bytes = someBytes
                    .Concat(compressed)
                    .Concat(GzipHeaderBytes.Take(partOfHeader))
                    .ToArray();
                var aBlockEndedWithPartHeaderBytes = bytes.Segment(someBytes.Length);

                var expectedPossiblePartOfHeaderAtTheEnd = bytes.Length - partOfHeader;
                var expectedBlock = new ArraySegment<byte>(aBlockEndedWithPartHeaderBytes.Array, someBytes.Length, compressed.Count);
                var expectedLeftBlock = aBlockEndedWithPartHeaderBytes.SliceFromTheEnd(partOfHeader);

                var buffer = new GzipBuffer(aBlockEndedWithPartHeaderBytes);

                var (cuttedBlock, leftBuffer) = buffer.CutFirstBlock();

                cuttedBlock.Should().BeEquivalentTo(expectedBlock);
                leftBuffer.Bytes.Should().BeEquivalentTo(expectedLeftBlock);
                leftBuffer.NoHeadersOrParts.Should().BeFalse();
                leftBuffer.PossiblePartOfHeaderAtTheEnd.Should().Be(expectedPossiblePartOfHeaderAtTheEnd);
            }

            [Fact]
            public void Should_return_bytes_segment_before_left_part_of_header_when_bytes_have_no_any_headers()
            {
                var someBytes = TestData.ShortFileContent.ToBytes();
                const int partOfHeader = 5;
                var noAnyHeadersdButPartHeaderBytes = someBytes
                    .Concat(GzipHeaderBytes.Take(partOfHeader))
                    .ToArray();

                var expectedBlock = new ArraySegment<byte>(noAnyHeadersdButPartHeaderBytes, 0, someBytes.Length);
                var expectedLeftBlock = noAnyHeadersdButPartHeaderBytes.SliceFromTheEnd(partOfHeader);

                var buffer = new GzipBuffer(noAnyHeadersdButPartHeaderBytes.ToSegment());

                var (firstBlock, leftBuffer) = buffer.CutFirstBlock();

                firstBlock.Should().BeEquivalentTo(expectedBlock);
                leftBuffer.Bytes.Should().BeEquivalentTo(expectedLeftBlock);
                leftBuffer.NoHeadersOrParts.Should().BeFalse();
                leftBuffer.PossiblePartOfHeaderAtTheEnd.Should().Be(noAnyHeadersdButPartHeaderBytes.Length - partOfHeader);
            }

            [Fact]
            public void Should_cut_nothing_when_bytes_contains_only_part_of_header()
            {
                var someBytes = TestData.ShortFileContent.ToBytes();
                var partOfHeaderBytes = someBytes
                    .Concat(GzipHeaderBytes.Take(5))
                    .ToArray()
                    .Segment(someBytes.Length);

                var buffer = new GzipBuffer(partOfHeaderBytes);

                var (firstBlock, leftBuffer) = buffer.CutFirstBlock();

                firstBlock.Count.Should().Be(0);
                leftBuffer.Bytes.Should().BeEquivalentTo(partOfHeaderBytes);
                leftBuffer.NoHeadersOrParts.Should().BeFalse();
                leftBuffer.PossiblePartOfHeaderAtTheEnd.Should().Be(someBytes.Length);
            }

            [Fact]
            public void Should_cut_only_header_when_bytes_contains_two_headers_with_empty_payload_between_them()
            {
                var twoEmptyBlocksBytes = GzipHeaderBytes.Concat(GzipHeaderBytes).ToArray().ToSegment();
                var expectedLeftBytes = twoEmptyBlocksBytes.ShiftOffsetRight(GzipHeaderBytes.Length);
                var buffer = new GzipBuffer(twoEmptyBlocksBytes);

                var (firstBlock, leftBuffer) = buffer.CutFirstBlock();

                firstBlock.Should().BeEquivalentTo(GzipHeaderBytes);
                leftBuffer.Headers.Should().HaveCount(1);
                leftBuffer.Bytes.Should().BeEquivalentTo(expectedLeftBytes);
                leftBuffer.NoHeadersOrParts.Should().BeFalse();
            }
        }

        private static IEnumerable<int> GetContentsOffsets(IEnumerable<ArraySegment<byte>> contents)
        {
            var offset = 0;
            foreach (var content in contents)
            {
                yield return offset;
                offset += content.Count;
            }
        }
    }
}