using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using FluentAssertions;
using NSubstitute;
using Parallel.Compression.Errors;
using Parallel.Compression.IO;
using Parallel.Compression.Tests.Helpers;
using Xunit;

namespace Parallel.Compression.Tests
{
    public class OutputFileSpec
    {
        private const string OffsetLabel = "========";

        private static MemoryStream CreateStreamMock()
        {
            var stream = Substitute.ForPartsOf<MemoryStream>();
            return stream;
        }

        [SuppressMessage("ReSharper", "ObjectCreationAsStatement")]
        [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
        public class Ctor
        {
            private readonly Stream stream;

            public Ctor()
            {
                stream = Substitute.For<Stream>();
                stream.CanRead.Returns(true);
                stream.CanSeek.Returns(true);
                stream.CanWrite.Returns(true);
            }

            [Fact]
            public void Should_fail_when_given_null_stream()
            {
                Action action = () => new OutputFile(null, OffsetLabel);

                action.Should().Throw<ArgumentNullException>();
            }

            [Fact]
            public void Should_fail_when_stream_can_not_seek()
            {
                stream.CanSeek.Returns(false);

                Action action = () => new OutputFile(stream, OffsetLabel);

                action.Should().Throw<ArgumentException>();
            }

            [Fact]
            public void Should_fail_when_stream_can_not_read()
            {
                stream.CanRead.Returns(false);

                Action action = () => new OutputFile(stream, OffsetLabel);

                action.Should().Throw<ArgumentException>();
            }

            [Fact]
            public void Should_fail_when_stream_can_not_write()
            {
                stream.CanWrite.Returns(false);

                Action action = () => new OutputFile(stream, OffsetLabel);

                action.Should().Throw<ArgumentException>();
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData(" ")]
            public void Should_fail_when_offset_label_is_null_or_empty(string label)
            {
                stream.CanWrite.Returns(false);

                Action action = () => new OutputFile(stream, label);

                action.Should().Throw<ArgumentException>();
            }

            [Fact]
            public void Should_not_fail_when_stream_can_write()
            {
                Action action = () => new OutputFile(stream, OffsetLabel);

                action.Should().NotThrow<Exception>();
            }
        }

        [SuppressMessage("ReSharper", "ImpureMethodCallOnReadonlyValueField")]
        public class Append
        {
            private readonly MemoryStream outputStream;
            private readonly ArraySegment<byte> bytes;
            private OutputFile outputFile;

            public Append()
            {
                var fileContent = TestData.InputFileContent;
                outputStream = new MemoryStream();
                outputFile = new OutputFile(outputStream, OffsetLabel);
                bytes = fileContent.Substring(0, 100).ToArraySegment();
            }

            [Fact]
            public void Should_fail_when_given_negative_offset()
            {
                Action action = () => outputFile.Append(bytes, -1);

                action.Should().Throw<ArgumentException>();
            }

            [Fact]
            public void Should_return_success_result_when_successfully_append_bytes()
            {
                var error = outputFile.Append(bytes, 0);

                error.Should().BeNull();
            }

            [Fact]
            public void Should_append_given_bytes()
            {
                outputFile.Append(bytes, 0);

                GetOutputBytes().Should().StartWith(bytes);
            }

            [Fact]
            public void Should_save_given_offset_at_the_end_of_the_block()
            {
                const long moreThanIntOffset = 16777215;
                var extpectedTrail = GetOffsetSectionBytes(moreThanIntOffset);

                outputFile.Append(bytes, moreThanIntOffset);

                GetOutputBytes().Should().EndWith(extpectedTrail);
                outputStream.Length.Should().Be(bytes.Count + extpectedTrail.Length);
            }

            [Fact]
            public void Should_add_new_portion_of_bytes_to_end_of_stream_and_replace_previous_offset()
            {
                var expectedTrail = GetOffsetSectionBytes(100);

                var bytes1 = bytes.Slice(0, 50);
                var bytes2 = bytes.Slice(50);
                var extpectedBytes = bytes1.Concat(bytes2).Concat(expectedTrail).ToArray();

                outputFile.Append(bytes1, 151);
                outputFile.Append(bytes2, 100);

                GetOutputBytes().Should().BeEquivalentTo(extpectedBytes);
            }

            [Fact]
            public void Should_replace_previous_offset_when_only_offset_stored_in_stream()
            {
                var expectedTrail = GetOffsetSectionBytes(100);

                var bytes2 = bytes.Slice(0, 50);
                var extpectedBytes = bytes2.Concat(expectedTrail).ToArray();

                outputFile.Append(default, 151);
                outputFile.Append(bytes2, 100);

                GetOutputBytes().Should().BeEquivalentTo(extpectedBytes);
            }

            [Fact]
            public void Should_rewind_file_to_the_end()
            {
                var expectedTrail = GetOffsetSectionBytes(10);

                var bytes1 = bytes.Slice(0, 50);
                var bytes2 = bytes.Slice(50);
                var extpectedBytes = bytes1.Concat(bytes2).Concat(expectedTrail).ToArray();

                outputFile.Append(bytes1, 0);
                outputStream.Position = 1;

                outputFile.Append(bytes2, 10);

                GetOutputBytes().Should().BeEquivalentTo(extpectedBytes);
            }

            [Fact]
            public void Should_return_error_when_fail_is_not_empty_and_does_not_end_with_offset_section()
            {
                var priviousBytes = new byte[] {1, 2, 3, 4};
                outputStream.WriteBytes(priviousBytes);

                var error = outputFile.Append(bytes, 110);

                error.Should().Be(ErrorCodes.OutputFileHaveWrongFormatOrAlreadyCommitted);
            }

            [Fact]
            public void Should_append_only_offset_if_given_bytes_is_empty()
            {
                var expectedTrail = GetOffsetSectionBytes(150);

                outputFile.Append(new ArraySegment<byte>(new byte[0]), 150);

                GetOutputBytes().Should().BeEquivalentTo(expectedTrail);
            }

            [Fact]
            public void Should_append_only_offset_if_given_bytes_is_default_array_segment()
            {
                var expectedTrail = GetOffsetSectionBytes(150);

                outputFile.Append(default, 150);

                GetOutputBytes().Should().BeEquivalentTo(expectedTrail);
            }

            [Fact]
            public void Should_rewrite_last_offset_with_new_offset_if_given_bytes_is_default()
            {
                var expectedTrail = GetOffsetSectionBytes(150);
                var expectedBytes = bytes.Concat(expectedTrail).ToArray();

                outputFile.Append(bytes, 111);
                outputFile.Append(default, 150);

                GetOutputBytes().Should().BeEquivalentTo(expectedBytes);
            }

            [Fact]
            public void Should_rewrite_last_offset_with_new_offset_if_given_bytes_is_empty()
            {
                var expectedTrail = GetOffsetSectionBytes(150);
                var expectedBytes = bytes.Concat(expectedTrail).ToArray();

                outputFile.Append(bytes, 111);
                outputFile.Append(new ArraySegment<byte>(new byte[0]), 150);

                GetOutputBytes().Should().BeEquivalentTo(expectedBytes);
            }

            [Fact]
            public void Should_return_error_when_file_not_found()
            {
                var failedFileStream = CreateStreamMock().ThrowsExceptionOnWrite(new FileNotFoundException());
                outputFile = new OutputFile(failedFileStream, OffsetLabel);

                var error = outputFile.Append(bytes, 0);

                error.Should().Be(ErrorCodes.OutputFileNotFound);
            }

            [Fact]
            public void Should_return_error_when_directory_not_found()
            {
                var failedFileStream = CreateStreamMock().ThrowsExceptionOnWrite(new DirectoryNotFoundException());
                outputFile = new OutputFile(failedFileStream, OffsetLabel);

                var result = outputFile.Append(bytes, 10);

                result.Should().Be(ErrorCodes.OutputFileDirectoryNotFound);
            }

            [Fact]
            public void Should_return_error_when_path_too_long()
            {
                var failedFileStream = CreateStreamMock().ThrowsExceptionOnWrite(new PathTooLongException());
                outputFile = new OutputFile(failedFileStream, OffsetLabel);

                var error = outputFile.Append(bytes, 10);

                error.Should().Be(ErrorCodes.OutputFilePathTooLong);
            }

            [Fact]
            public void Should_return_error_when_file_write_unauthorized()
            {
                var failedFileStream = CreateStreamMock().ThrowsExceptionOnWrite(new UnauthorizedAccessException());
                outputFile = new OutputFile(failedFileStream, OffsetLabel);

                var error = outputFile.Append(bytes, 10);

                error.Should().Be(ErrorCodes.OutputFileReadUnauthorized);
            }

            [Fact]
            public void Should_fail_when_stream_already_disposed()
            {
                outputStream.Dispose();

                Action action = () => outputFile.Append(bytes, 10);

                action.Should().Throw<ObjectDisposedException>();
            }

            private byte[] GetOutputBytes()
            {
                return outputStream.ToArray();
            }

            private static byte[] GetOffsetSectionBytes(long offset)
            {
                return Encoding.ASCII.GetBytes(OffsetLabel)
                    .Concat(BitConverter.GetBytes(offset))
                    .ToArray();
            }
        }

        public class Commit
        {
            private readonly MemoryStream outputStream;
            private readonly string fileContent;
            private readonly ArraySegment<byte> bytes;
            private readonly OutputFile outputFile;

            public Commit()
            {
                fileContent = TestData.InputFileContent;
                outputStream = CreateStreamMock();
                outputFile = new OutputFile(outputStream, OffsetLabel);
                bytes = fileContent.Substring(0, 100).ToArraySegment();
            }

            [Fact]
            public void Should_return_error_and_not_affect_stream_when_steam_is_empty()
            {
                var error = outputFile.Commit();

                error.Should().Be(ErrorCodes.OutputFileHaveWrongFormatOrAlreadyCommitted);
                outputStream.Length.Should().Be(0);
            }

            [Fact]
            public void Should_return_error_and_not_affect_stream_when_steam_does_not_ends_with_label()
            {
                var contentBeforeCommit = fileContent.Substring(0, 100).ToBytes();
                outputStream.WriteBytes(contentBeforeCommit);

                var error = outputFile.Commit();

                error.Should().Be(ErrorCodes.OutputFileHaveWrongFormatOrAlreadyCommitted);
                outputStream.ToArray().Should().BeEquivalentTo(contentBeforeCommit);
            }

            [Fact]
            public void Should_remove_last_offset_section_when_stream_contains_only_offset_section()
            {
                outputStream.WriteString(OffsetLabel).WriteLong(100);

                var error = outputFile.Commit();

                error.Should().BeNull();
                outputStream.ToArray().Should().BeEmpty();
            }

            [Fact]
            public void Should_remove_last_offset_section()
            {
                var contentBeforeCommit = fileContent.Substring(0, 100).ToArraySegment();
                outputFile.Append(contentBeforeCommit, 100);

                var error = outputFile.Commit();

                error.Should().BeNull();
                outputStream.ToArray().Should().BeEquivalentTo(contentBeforeCommit);
            }

            [Theory]
            [InlineData(StreamOps.Read)]
            [InlineData(StreamOps.SetLength)]
            public void Should_return_error_when_file_not_found(StreamOps operation)
            {
                outputFile.Append(bytes, 10);
                outputStream.ThrowsExceptionOn(operation, new FileNotFoundException());

                var error = outputFile.Commit();

                error.Should().Be(ErrorCodes.OutputFileNotFound);
            }

            [Theory]
            [InlineData(StreamOps.Read)]
            [InlineData(StreamOps.SetLength)]
            public void Should_return_error_when_directory_not_found(StreamOps operation)
            {
                outputFile.Append(bytes, 10);
                outputStream.ThrowsExceptionOn(operation, new DirectoryNotFoundException());

                var result = outputFile.Commit();

                result.Should().Be(ErrorCodes.OutputFileDirectoryNotFound);
            }

            [Theory]
            [InlineData(StreamOps.Read)]
            [InlineData(StreamOps.SetLength)]
            public void Should_return_error_when_path_too_long(StreamOps operation)
            {
                outputFile.Append(bytes, 10);
                outputStream.ThrowsExceptionOn(operation, new PathTooLongException());

                var error = outputFile.Commit();

                error.Should().Be(ErrorCodes.OutputFilePathTooLong);
            }

            [Theory]
            [InlineData(StreamOps.Read)]
            [InlineData(StreamOps.SetLength)]
            public void Should_return_error_when_file_write_unauthorized(StreamOps operation)
            {
                outputFile.Append(bytes, 10);
                outputStream.ThrowsExceptionOn(operation, new UnauthorizedAccessException());

                var error = outputFile.Commit();

                error.Should().Be(ErrorCodes.OutputFileReadUnauthorized);
            }

            [Fact]
            public void Should_fail_when_stream_already_disposed()
            {
                outputStream.Dispose();

                Action action = () => outputFile.Append(bytes, 10);

                action.Should().Throw<ObjectDisposedException>();
            }
        }

        public class GetLastOffset
        {
            private readonly MemoryStream outputStream;
            private readonly string fileContent;
            private readonly ArraySegment<byte> bytes;
            private readonly OutputFile outputFile;

            public GetLastOffset()
            {
                fileContent = TestData.InputFileContent;
                outputStream = CreateStreamMock();
                outputFile = new OutputFile(outputStream, OffsetLabel);
                bytes = fileContent.Substring(0, 100).ToArraySegment();
            }

            [Fact]
            public void Should_return_0_when_stream_is_empty()
            {
                var (lastOffset, error) = outputFile.GetLastOffset();

                error.Should().BeNull();
                lastOffset.Should().Be(0);
            }

            [Fact]
            public void Should_return_offset_after_first_append_block()
            {
                outputFile.Append(bytes, 100);

                var (lastOffset, error) = outputFile.GetLastOffset();

                error.Should().BeNull();
                lastOffset.Should().Be(100);
            }

            [Fact]
            public void Should_return_offset_after_each_appended_block()
            {
                outputFile.Append(bytes, 100);
                var lastOffset1 = outputFile.GetLastOffset().Value;

                outputFile.Append(bytes, 200);
                var lastOffset2 = outputFile.GetLastOffset().Value;

                outputFile.Append(bytes, 300);
                var lastOffset3 = outputFile.GetLastOffset().Value;

                lastOffset1.Should().Be(100);
                lastOffset2.Should().Be(200);
                lastOffset3.Should().Be(300);
            }

            [Fact]
            public void Should_return_error_when_file_is_not_ended_with_offset_section()
            {
                outputStream.WriteString(fileContent);

                var (_, error) = outputFile.GetLastOffset();

                error.Should().Be(ErrorCodes.OutputFileHaveWrongFormatOrAlreadyCommitted);
            }

            [Theory]
            [InlineData(StreamOps.Read)]
            [InlineData(StreamOps.Seek)]
            public void Should_return_error_when_file_not_found(StreamOps operation)
            {
                outputFile.Append(bytes, 10);
                outputStream.ThrowsExceptionOn(operation, new FileNotFoundException());

                var (_, error) = outputFile.GetLastOffset();

                error.Should().Be(ErrorCodes.OutputFileNotFound);
            }

            [Theory]
            [InlineData(StreamOps.Read)]
            [InlineData(StreamOps.Seek)]
            public void Should_return_error_when_directory_not_found(StreamOps operation)
            {
                outputFile.Append(bytes, 10);
                outputStream.ThrowsExceptionOn(operation, new DirectoryNotFoundException());

                var (_, error) = outputFile.GetLastOffset();

                error.Should().Be(ErrorCodes.OutputFileDirectoryNotFound);
            }

            [Theory]
            [InlineData(StreamOps.Read)]
            [InlineData(StreamOps.Seek)]
            public void Should_return_error_when_path_too_long(StreamOps operation)
            {
                outputFile.Append(bytes, 10);
                outputStream.ThrowsExceptionOn(operation, new PathTooLongException());

                var (_, error) = outputFile.GetLastOffset();

                error.Should().Be(ErrorCodes.OutputFilePathTooLong);
            }

            [Theory]
            [InlineData(StreamOps.Read)]
            [InlineData(StreamOps.Seek)]
            public void Should_return_error_when_file_write_unauthorized(StreamOps operation)
            {
                outputFile.Append(bytes, 10);
                outputStream.ThrowsExceptionOn(operation, new UnauthorizedAccessException());

                var (_, error) = outputFile.GetLastOffset();

                error.Should().Be(ErrorCodes.OutputFileReadUnauthorized);
            }

            [Fact]
            public void Should_fail_when_stream_already_disposed()
            {
                outputStream.Dispose();

                Action action = () => outputFile.GetLastOffset();

                action.Should().Throw<ObjectDisposedException>();
            }
        }

        public class CompressionRatio
        {
            private readonly MemoryStream outputStream;
            private readonly string fileContent;
            private readonly OutputFile outputFile;

            public CompressionRatio()
            {
                fileContent = TestData.InputFileContent;
                outputStream = CreateStreamMock();
                outputFile = new OutputFile(outputStream, OffsetLabel);
            }

            [Fact]
            public void Should_return_percentages_of_compression_when_source_file_is_bugger()
            {
                outputStream.WriteString(fileContent);

                var compressionRatio = outputFile.CompressionRatio(fileContent.Length*2);

                compressionRatio.Should().Be(50);
            }

            [Fact]
            public void Should_return_percentages_of_compression_when_source_file_have_equal_length()
            {
                outputStream.WriteString(fileContent);

                var compressionRatio = outputFile.CompressionRatio(fileContent.Length);

                compressionRatio.Should().Be(100);
            }

            [Fact]
            public void Should_return_percentages_of_compression_when_source_is_smaller()
            {
                outputStream.WriteString(fileContent);

                var compressionRatio = outputFile.CompressionRatio((int) (fileContent.Length*0.5));

                compressionRatio.Should().Be(200);
            }
        }
    }
}