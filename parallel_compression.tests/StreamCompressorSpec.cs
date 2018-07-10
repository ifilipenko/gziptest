using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using FluentAssertions;
using NSubstitute;
using Parallel.Compression.Compression;
using Parallel.Compression.Configuration;
using Parallel.Compression.Errors;
using Parallel.Compression.Logging;
using Parallel.Compression.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Parallel.Compression.Tests
{
    public class StreamCompressorSpec
    {
        [SuppressMessage("ReSharper", "ObjectCreationAsStatement")]
        [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
        public class Ctor
        {
            private readonly IBlockCompression compression;
            private readonly CompressorSettings settings;
            private readonly ILog log;

            public Ctor()
            {
                log = Substitute.For<ILog>();
                settings = new CompressorSettingsBuilder()
                    .SetDefaultInputFileReadingBufferSize()
                    .SetDefaultOffsetLabel()
                    .SetDefaultPararllelism()
                    .GetSettings();
                compression = Substitute.For<IBlockCompression>();
            }

            [Fact]
            public void Should_fail_when_given_null_compression()
            {
                Action action = () => new StreamCompressor(null, settings, log);

                action.Should().Throw<ArgumentNullException>();
            }

            [Fact]
            public void Should_fail_when_given_null_settings()
            {
                Action action = () => new StreamCompressor(compression, null, log);

                action.Should().Throw<ArgumentNullException>();
            }

            [Fact]
            public void Should_fail_when_given_null_log()
            {
                Action action = () => new StreamCompressor(compression, settings, null);

                action.Should().Throw<ArgumentNullException>();
            }
        }

        public class Compress
        {
            private readonly ITestOutputHelper output;
            private const int InputStreamReadSize = 512;
            private readonly StreamCompressor streamCompressor;

            public Compress(ITestOutputHelper output)
            {
                this.output = output;
                var gzipBlockCompression = new GzipBlockCompression();

                var compressorSettings = new CompressorSettingsBuilder()
                    .SetDefaultOffsetLabel()
                    .SetInputFileReadingBufferSize(InputStreamReadSize)
                    .SetParallelismByThreadsPerCpu(4)
                    .GetSettings();
                streamCompressor = new StreamCompressor(gzipBlockCompression, compressorSettings, new TestLog(output));
            }

            [Fact]
            [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
            public void Should_fail_when_given_null_input_stream()
            {
                Action action = () => streamCompressor.Compress(null, new MemoryStream());

                action.Should().Throw<ArgumentNullException>();
            }

            [Fact]
            [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
            public void Should_fail_when_given_null_output_stream()
            {
                Action action = () => streamCompressor.Compress(new MemoryStream(), null);

                action.Should().Throw<ArgumentNullException>();
            }

            [Fact]
            public void Should_compress_nothing_when_given_empty_stream()
            {
                var inputStream = new MemoryStream();
                var outputStream = new MemoryStream();

                var (_, error) = streamCompressor.Compress(inputStream, outputStream);

                outputStream.Length.Should().Be(0);
                error.Should().Be(ErrorCodes.NothingToCompress);
            }

            [Fact]
            public void Should_compress_nothing_when_given_ended_stream()
            {
                var inputText = TestData.InputFileContent;
                var inputStream = inputText.AsStream();
                inputStream.Seek(0, SeekOrigin.End);
                var outputStream = new MemoryStream();

                var (_, error) = streamCompressor.Compress(inputStream, outputStream);

                outputStream.Length.Should().Be(0);
                error.Should().Be(ErrorCodes.NothingToCompress);
            }

            [Fact]
            public void Should_compress_by_blocks_with_respect_their_orders()
            {
                var inputText = string.Join("", Enumerable.Range(0, 40).Select(x => x + " " + TestData.InputFileContent));
                var inputStream = inputText.AsStream();
                var outputStream = new MemoryStream();

                streamCompressor.Compress(inputStream, outputStream);

                outputStream.Length.Should().BeGreaterThan(0).And.BeLessThan(inputStream.Length);
                outputStream.SeekToBegin().DecompressToString().Should().Be(inputText);
            }

            [Fact]
            public void Should_return_compression_ratio()
            {
                var inputText = string.Join("", Enumerable.Range(0, 40).Select(x => x + " " + TestData.InputFileContent));
                var inputStream = inputText.AsStream();
                var outputStream = new MemoryStream();

                var (ratio, error) = streamCompressor.Compress(inputStream, outputStream);

                output.WriteLine($"Current ratio is {ratio}");
                ratio.Should().BeGreaterThan(0);
                error.Should().BeNull();
            }
        }
    }
}