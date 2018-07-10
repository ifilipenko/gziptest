using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using FluentAssertions;
using Parallel.Compression.Compression;
using Parallel.Compression.GzipFormat;
using Parallel.Compression.Helpers;
using Parallel.Compression.Tests.Helpers;
using Xunit;

namespace Parallel.Compression.Tests
{
    public class GzipHeaderSpec
    {
        public class Default
        {
            private readonly GzipHeader defaultInstance = default;

            [Fact]
            public void Should_return_empty_mime_type_bytes()
            {
                defaultInstance.MimetypeBytes.Array.Should().BeNull();
            }

            [Fact]
            public void Should_fail_when_try_to_set_mimetype_bytes()
            {
                Action action = () => defaultInstance.SetMimetypeBytes(new byte[4]);
                
                action.Should().Throw<InvalidOperationException>();
            }
        }

        [SuppressMessage("ReSharper", "PossibleInvalidOperationException")]
        public class FindFirst
        {
            [Fact]
            public void Should_return_null_when_bytes_does_not_contain_gzip_header()
            {
                var bytes = TestData.ShortFileContent.ToArraySegment();

                var result = GzipHeader.FindFirst(bytes);

                result.Should().BeNull();
            }

            [Fact]
            public void Should_return_gzip_header_when_bytes_starts_with_gzip_header()
            {
                var bytes = TestData.ShortFileContent.GzipCompress();

                var result = GzipHeader.FindFirst(bytes);

                result.Should().NotBeNull();
                result.Value.Position.Should().Be(0);
            }

            [Fact]
            public void Should_return_gzip_header_when_it_first_occurence_not_from_start_of_bytes()
            {
                var someUncompressedBytes = TestData.ShortFileContent.ToBytes();
                var bytes = someUncompressedBytes.Concat(TestData.ShortFileContent.GzipCompress())
                    .Concat(TestData.ShortFileContent.GzipCompress())
                    .ToArray()
                    .ToSegment();

                var result = GzipHeader.FindFirst(bytes);

                result.Should().NotBeNull();
                result.Value.Position.Should().Be(someUncompressedBytes.Length);
            }
            
            [Fact]
            public void Should_return_gzip_header_when_bytes_have_size_exactly_equal_to_header()
            {
                var bytes = TestData.ShortFileContent.GzipCompress().TakeFirst(GzipHeader.Length);

                var result = GzipHeader.FindFirst(bytes);

                result.Should().NotBeNull();
                result.Value.Position.Should().Be(0);
            }

            [Fact]
            public void Should_set_position_of_found_header_with_respecting_segment_offset()
            {
                var uncompressedBytes = TestData.ShortFileContent.ToBytes();
                var firstCompressed = TestData.ShortFileContent.GzipCompress();
                var secondCompressed = TestData.ShortFileContent.GzipCompress();
                var bytes = uncompressedBytes
                    .Concat(firstCompressed)
                    .Concat(secondCompressed)
                    .ToArray()
                    .Segment(uncompressedBytes.Length + 3);

                var result = GzipHeader.FindFirst(bytes);

                result.Should().NotBeNull();
                result.Value.Position.Should().Be(uncompressedBytes.Length + firstCompressed.Count);
            }

            [Fact]
            public void Should_return_null_when_given_segment_have_less_count_than_header_required()
            {
                var someUncompressedBytes = TestData.ShortFileContent.ToBytes();
                var firstCompressed = TestData.ShortFileContent.GzipCompress();
                var bytes = someUncompressedBytes.Concat(firstCompressed)
                    .ToArray()
                    .Segment(someUncompressedBytes.Length, 8);

                var result = GzipHeader.FindFirst(bytes);

                result.Should().BeNull();
            }

            [Fact]
            public void Should_return_null_when_given_default_segment()
            {
                var result = GzipHeader.FindFirst(default);

                result.Should().BeNull();
            }
        }

        public class IsPrefixFor
        {
            [Fact]
            public void Should_indicate_that_bytes_not_starts_with_header()
            {
                var bytes = TestData.ShortFileContent.ToArraySegment();

                var result = GzipHeader.IsPrefixFor(bytes);

                result.Should().BeFalse();
            }

            [Fact]
            public void Should_indicate_that_segment_not_point_to_gzip_header()
            {
                var bytes = TestData.ShortFileContent.GzipCompress().Slice(2);

                var result = GzipHeader.IsPrefixFor(bytes);

                result.Should().BeFalse();
            }

            [Fact]
            public void Should_indicate_that_bytes_is_started_with_gzip_header()
            {
                var bytes = TestData.ShortFileContent.GzipCompress();

                var result = GzipHeader.IsPrefixFor(bytes);

                result.Should().BeTrue();
            }

            [Fact]
            public void Should_return_false_when_given_default_segment()
            {
                var result = GzipHeader.IsPrefixFor(default);

                result.Should().BeFalse();
            }
        }

        public class MimetypeBytes
        {
            private readonly GzipHeader gzipHeader;
            private readonly int headerPosition;
            private readonly byte[] bytes;

            [SuppressMessage("ReSharper", "PossibleInvalidOperationException")]
            public MimetypeBytes()
            {
                var uncompressedData = TestData.ShortFileContent.ToBytes();
                headerPosition = uncompressedData.Length;
                bytes = uncompressedData
                    .Concat(TestData.ShortFileContent.GzipCompress())
                    .ToArray();

                gzipHeader = GzipHeader.FindFirst(bytes).Value;
            }

            [Fact]
            public void Should_return_4_bytes_from_4th_byte_of_header_array()
            {
                var mimetypeBytes = gzipHeader.MimetypeBytes;

                mimetypeBytes.Offset.Should().Be(headerPosition + 4);
                mimetypeBytes.Count.Should().Be(4);
                mimetypeBytes.Array.Should().BeSameAs(bytes);
            }

            [Fact]
            public void Should_be_0_bytes_in_mimetype_by_default()
            {
                var mimetypeBytes = gzipHeader.MimetypeBytes;
                
                mimetypeBytes.ToArray().Should().BeEquivalentTo(new byte[] {0, 0, 0, 0});
            }

            [Fact]
            public void Should_set_mimetype_into_header_and_update_underlying_array()
            {
                var mimetypeSegment = gzipHeader.MimetypeBytes;
                var expectedMimetypeBytes = new byte[] {1, 2, 3, 4};
                
                gzipHeader.SetMimetypeBytes(expectedMimetypeBytes);

                mimetypeSegment.ToArray().Should().BeEquivalentTo(expectedMimetypeBytes);
                bytes.Segment(mimetypeSegment.Offset, mimetypeSegment.Count).ToArray().Should().BeEquivalentTo(expectedMimetypeBytes);
            }

            [Fact]
            [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
            public void Should_fail_when_given_null_bytes()
            {
                Action action = () => gzipHeader.SetMimetypeBytes(null);

                action.Should().Throw<ArgumentNullException>();
            }
            
            [Fact]
            public void Should_fail_when_given_greater_bytes_count_than_4()
            {
                Action action = () => gzipHeader.SetMimetypeBytes(new byte[5]);

                action.Should().Throw<ArgumentException>();
            }
            
            [Fact]
            public void Should_fail_when_given_less_bytes_count_than_4()
            {
                Action action = () => gzipHeader.SetMimetypeBytes(new byte[1]);

                action.Should().Throw<ArgumentException>();
            }
        }
    }
}