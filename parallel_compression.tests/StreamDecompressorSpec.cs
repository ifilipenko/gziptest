using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using FluentAssertions;
using NSubstitute;
using Parallel.Compression.Configuration;
using Parallel.Compression.Decompression;
using Parallel.Compression.Errors;
using Parallel.Compression.Helpers;
using Parallel.Compression.Logging;
using Parallel.Compression.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Parallel.Compression.Tests
{
    public class StreamDecompressorSpec
    {
        [SuppressMessage("ReSharper", "ObjectCreationAsStatement")]
        [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
        public class Ctor
        {
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
            }

            [Fact]
            public void Should_fail_when_given_null_settings()
            {
                Action action = () => new StreamDecompressor(null, log);

                action.Should().Throw<ArgumentNullException>();
            }

            [Fact]
            public void Should_fail_when_given_null_log()
            {
                Action action = () => new StreamDecompressor(settings, null);

                action.Should().Throw<ArgumentNullException>();
            }
        }

        public class Compress
        {
            private readonly ITestOutputHelper output;
            private readonly StreamDecompressor decompressor;

            public Compress(ITestOutputHelper output)
            {
                this.output = output;

                var compressorSettings = new CompressorSettingsBuilder()
                    .SetDefaultOffsetLabel()
                    .SetInputFileReadingBufferSize(84.Kilobytes())
                    .SetParallelismByThreadsPerCpu(4)
                    .GetSettings();
                decompressor = new StreamDecompressor(compressorSettings, new TestLog(output));
            }

            [Fact]
            [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
            public void Should_fail_when_given_null_input_stream()
            {
                Action action = () => decompressor.Decompress(null, new MemoryStream());

                action.Should().Throw<ArgumentNullException>();
            }

            [Fact]
            [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
            public void Should_fail_when_given_null_output_stream()
            {
                Action action = () => decompressor.Decompress(new MemoryStream(), null);

                action.Should().Throw<ArgumentNullException>();
            }

            [Fact]
            public void Should_do_nothing_when_given_empty_stream()
            {
                var inputStream = new MemoryStream();
                var outputStream = new MemoryStream();

                var (_, error) = decompressor.Decompress(inputStream, outputStream);

                outputStream.Length.Should().Be(0);
                error.Should().Be(ErrorCodes.NothingToDecompress);
            }

            [Fact]
            public void Should_decompress_nothing_when_given_ended_stream()
            {
                var inputText = TestData.InputFileContent;
                var inputStream = inputText.AsStream();
                inputStream.Seek(0, SeekOrigin.End);
                var outputStream = new MemoryStream();

                var (_, error) = decompressor.Decompress(inputStream, outputStream);

                outputStream.Length.Should().Be(0);
                error.Should().Be(ErrorCodes.NothingToDecompress);
            }

            [Fact]
            public void Should_decompress_by_blocks_with_respect_their_orders()
            {
                var blocks = Enumerable.Range(0, 40).Select(x => x + " " + TestData.InputFileContent).ToArray();
                var expectedText = string.Join("", blocks);
                var inputStream = blocks.SelectMany(x => x.GzipCompress()).ToArray().AsStream();
                var outputStream = new MemoryStream();

                decompressor.Decompress(inputStream, outputStream);

                outputStream.SeekToBegin().ReadToString().Should().Be(expectedText);
            }

            [Fact]
            public void Should_return_compression_ratio()
            {
                var blocks = Enumerable.Range(0, 40).Select(x => x + " " + TestData.InputFileContent).ToArray();
                var inputStream = blocks.SelectMany(x => x.GzipCompress()).ToArray().AsStream();
                var outputStream = new MemoryStream();

                var (ratio, error) = decompressor.Decompress(inputStream, outputStream);

                output.WriteLine($"Current ratio is {ratio}");
                ratio.Should().BeGreaterThan(100);
                error.Should().BeNull();
            }
        }
    }
}