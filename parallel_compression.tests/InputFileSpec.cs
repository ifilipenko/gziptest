using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Parallel.Compression.Errors;
using Parallel.Compression.IO;
using Parallel.Compression.Tests.Helpers;
using Xunit;

namespace Parallel.Compression.Tests
{
    public class InputFileSpec
    {
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
            }

            [Fact]
            public void Should_fail_when_given_null_stream()
            {
                Action action = () => new InputFile(null);

                action.Should().Throw<ArgumentNullException>();
            }

            [Fact]
            public void Should_fail_when_stream_can_not_seek()
            {
                stream.CanSeek.Returns(false);

                Action action = () => new InputFile(stream);

                action.Should().Throw<ArgumentException>();
            }

            [Fact]
            public void Should_fail_when_stream_can_not_read()
            {
                stream.CanRead.Returns(false);

                Action action = () => new InputFile(stream);

                action.Should().Throw<ArgumentException>();
            }

            [Fact]
            public void Should_not_fail_when_stream_can_read_and_seek()
            {
                Action action = () => new InputFile(stream);

                action.Should().NotThrow<Exception>();
            }
        }

        public class ReadTo
        {
            private readonly MemoryStream inputStream;
            private InputFile inputFile;
            private readonly string fileContent;

            public ReadTo()
            {
                fileContent = TestData.InputFileContent;
                inputStream = TestData.GenerateStream(fileContent);
                inputFile = new InputFile(inputStream);
            }

            [Fact]
            [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
            public void Should_fail_when_given_null_buffer()
            {
                Action action = () => inputFile.ReadTo(null);

                action.Should().Throw<ArgumentNullException>();
            }

            [Fact]
            public void Should_fail_when_given_empty_buffer()
            {
                Action action = () => inputFile.ReadTo(new byte[0]);

                action.Should().Throw<ArgumentException>();
            }

            [Fact]
            public void Should_return_success_result_when_successfully_read_whole_buffer_from_stream()
            {
                var buffer = new byte[100];
                var expectedBuffer = Encoding.ASCII.GetBytes(fileContent.Substring(0, 100));

                var result = inputFile.ReadTo(buffer);

                result.IsSuccessful.Should().BeTrue();
                result.Value.Should().Be(100);
                buffer.Should().BeEquivalentTo(expectedBuffer);
            }

            [Fact]
            public void Should_return_success_result_when_successfully_read_stream_trail_into_buffer()
            {
                var buffer = new byte[100];
                inputStream.Position = inputStream.Length - 50;
                var expectedBuffer = Encoding.ASCII.GetBytes(fileContent.Substring(fileContent.Length - 50))
                    .Concat(new byte[50]).ToArray();

                var result = inputFile.ReadTo(buffer);

                result.IsSuccessful.Should().BeTrue();
                result.Value.Should().Be(50);
                buffer.Should().BeEquivalentTo(expectedBuffer);
            }

            [Fact]
            public void Should_return_successful_result_with_0_when_read_stream_from_end()
            {
                inputStream.Position = inputStream.Length;
                var buffer = new byte[100];
                var expectedBuffer = new byte[100];

                var result = inputFile.ReadTo(buffer);

                result.IsSuccessful.Should().BeTrue();
                result.Value.Should().Be(0);
                buffer.Should().BeEquivalentTo(expectedBuffer);
            }

            [Fact]
            public void Should_return_error_when_file_not_found()
            {
                var failedFileStream = CreateStreamMock();
                failedFileStream.Read(Any.Buffer, Any.Offset, Any.Count).Throws(new FileNotFoundException());
                inputFile = new InputFile(failedFileStream);

                var result = inputFile.ReadTo(new byte[100]);

                result.IsFailed.Should().BeTrue();
                result.FailureValue.Should().Be(ErrorCodes.InputFileNotFound);
            }

            [Fact]
            public void Should_return_error_when_directory_not_found()
            {
                var failedFileStream = CreateStreamMock();
                failedFileStream.Read(Any.Buffer, Any.Offset, Any.Count).Throws(new DirectoryNotFoundException());
                inputFile = new InputFile(failedFileStream);

                var result = inputFile.ReadTo(new byte[100]);

                result.IsFailed.Should().BeTrue();
                result.FailureValue.Should().Be(ErrorCodes.InputFileDirectoryNotFound);
            }

            [Fact]
            public void Should_return_error_when_path_too_long()
            {
                var failedFileStream = CreateStreamMock();
                failedFileStream.Read(Any.Buffer, Any.Offset, Any.Count).Throws(new PathTooLongException());
                inputFile = new InputFile(failedFileStream);

                var result = inputFile.ReadTo(new byte[100]);

                result.IsFailed.Should().BeTrue();
                result.FailureValue.Should().Be(ErrorCodes.InputFilePathTooLong);
            }

            [Fact]
            public void Should_return_error_when_file_read_unauthorized()
            {
                var failedFileStream = CreateStreamMock();
                failedFileStream.Read(Any.Buffer, Any.Offset, Any.Count).Throws(new UnauthorizedAccessException());
                inputFile = new InputFile(failedFileStream);

                var result = inputFile.ReadTo(new byte[100]);

                result.IsFailed.Should().BeTrue();
                result.FailureValue.Should().Be(ErrorCodes.InputFileReadUnauthorized);
            }

            [Fact]
            public void Should_fail_when_stream_already_disposed()
            {
                inputStream.Dispose();

                Action action = () => inputFile.ReadTo(new byte[100]);

                action.Should().Throw<ObjectDisposedException>();
            }

            private static Stream CreateStreamMock()
            {
                var stream = Substitute.For<Stream>();
                stream.CanRead.Returns(true);
                stream.CanSeek.Returns(true);
                return stream;
            }
        }

        public class SeekTo
        {
            private readonly MemoryStream inputStream;
            private InputFile inputFile;

            public SeekTo()
            {
                var fileContent = TestData.InputFileContent;
                inputStream = TestData.GenerateStream(fileContent);
                inputFile = new InputFile(inputStream);
            }

            [Fact]
            public void Should_set_position_to_specified_offset()
            {
                inputStream.Position = 150;

                inputFile.SeekTo(100);

                inputStream.Position.Should().Be(100);
            }

            [Fact]
            public void Should_not_return_error_when_successfully_set_new_position()
            {
                var error = inputFile.SeekTo(100);

                error.Should().BeNull();
            }

            [Fact]
            public void Should_return_error_when_file_not_found()
            {
                var failedFileStream = CreateStreamMock();
                failedFileStream.Seek(Any.Long, SeekOrigin.Begin).Throws(new FileNotFoundException());
                inputFile = new InputFile(failedFileStream);

                var result = inputFile.SeekTo(100);

                result.Should().Be(ErrorCodes.InputFileNotFound);
            }

            [Fact]
            public void Should_return_error_when_directory_not_found()
            {
                var failedFileStream = CreateStreamMock();
                failedFileStream.Seek(Any.Long, SeekOrigin.Begin).Throws(new DirectoryNotFoundException());
                inputFile = new InputFile(failedFileStream);

                var result = inputFile.SeekTo(100);

                result.Should().Be(ErrorCodes.InputFileDirectoryNotFound);
            }

            [Fact]
            public void Should_return_error_when_path_too_long()
            {
                var failedFileStream = CreateStreamMock();
                failedFileStream.Seek(Any.Long, SeekOrigin.Begin).Throws(new PathTooLongException());
                inputFile = new InputFile(failedFileStream);

                var result = inputFile.SeekTo(100);

                result.Should().Be(ErrorCodes.InputFilePathTooLong);
            }

            [Fact]
            public void Should_return_error_when_file_read_unauthorized()
            {
                var failedFileStream = CreateStreamMock();
                failedFileStream.Seek(Any.Long, SeekOrigin.Begin).Throws(new UnauthorizedAccessException());
                inputFile = new InputFile(failedFileStream);

                var result = inputFile.SeekTo(100);

                result.Should().Be(ErrorCodes.InputFileReadUnauthorized);
            }

            [Fact]
            public void Should_fail_when_stream_already_disposed()
            {
                inputStream.Dispose();

                Action action = () => inputFile.SeekTo(100);

                action.Should().Throw<ObjectDisposedException>();
            }

            private static Stream CreateStreamMock()
            {
                var stream = Substitute.For<Stream>();
                stream.CanRead.Returns(true);
                stream.CanSeek.Returns(true);
                return stream;
            }
        }
    }
}
