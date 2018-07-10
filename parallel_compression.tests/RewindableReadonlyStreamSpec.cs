using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Parallel.Compression.Decompression;
using Parallel.Compression.Decompression.Streams;
using Parallel.Compression.Helpers;
using Parallel.Compression.Tests.Helpers;
using Xunit;

namespace Parallel.Compression.Tests
{
    public class RewindableReadonlyStreamSpec
    {
        public class Ctor
        {
            [Fact]
            public void Should_initialize_stream_position_with_current_position_of_given_stream()
            {
                var stream = TestData.InputFileContent.ToBytes().AsStream();
                stream.Position = stream.Length/2;
                
                var rewindableReadonlyStream = new RewindableReadonlyStream(stream);

                rewindableReadonlyStream.Position.Should().Be(stream.Length/2);
            }
        }

        public class Read
        {
            [Fact]
            public void Should_able_to_read_file_to_end()
            {
                var stream = TestData.InputFileContent.ToBytes().AsStream();
                var rewindableReadonlyStream = new RewindableReadonlyStream(stream);
                var buffer = new byte[stream.Length];

                rewindableReadonlyStream.ReadExactFullBuffer(buffer);

                stream.Position.Should().Be(stream.Length);
                buffer.AsString().Should().Be(TestData.InputFileContent);
            }

            [Fact]
            public void Should_change_current_poistion_while_read()
            {
                var stream = TestData.InputFileContent.ToBytes().AsStream();
                var rewindableReadonlyStream = new RewindableReadonlyStream(stream);
                var buffer = new byte[stream.Length/3];
                var sourseStreamPositions = new List<long>();
                var positions = new List<long>();
                var expectedPositions = new List<long>();

                var offset = 0;
                int read;
                while((read = rewindableReadonlyStream.Read(buffer)) > 0)
                {
                    sourseStreamPositions.Add(stream.Position);
                    positions.Add(rewindableReadonlyStream.Position);
                    expectedPositions.Add(offset += read);
                }

                positions.Should().BeEquivalentTo(sourseStreamPositions);
                positions.Should().BeEquivalentTo(expectedPositions);
            }
        }
        
        public class ReturnTailOfReadedBytes
        {
            [Fact]
            public void Should_return_readed_bytes_and_rollback_position()
            {
                var stream = TestData.InputFileContent.ToBytes().AsStream();
                var rewindableReadonlyStream = new RewindableReadonlyStream(stream);
                var buffer = new byte[stream.Length];
                var slices = buffer.Slices(buffer.Length/2 + 1).ToArray();
                
                rewindableReadonlyStream.Read(buffer);
                var beforeReturn = rewindableReadonlyStream.Position;

                rewindableReadonlyStream.ReturnTailOfReadedBytes(slices[1]);
                var afterReturn1 = rewindableReadonlyStream.Position;
                
                rewindableReadonlyStream.ReturnTailOfReadedBytes(slices[0]);
                var afterReturn2 = rewindableReadonlyStream.Position;

                beforeReturn.Should().Be(stream.Length);
                afterReturn1.Should().Be(slices[0].Count);
                afterReturn2.Should().Be(0);
                stream.Position.Should().Be(stream.Length);
            }

            [Fact]
            public void Should_read_only_returned_bytes_or_buffer_size()
            {
                var stream = TestData.InputFileContent.ToBytes().AsStream();
                var rewindableReadonlyStream = new RewindableReadonlyStream(stream);
                var buffer = new byte[stream.Length];
                var slices = buffer.Slices(buffer.Length/2 + 1).ToArray();
                
                rewindableReadonlyStream.Read(buffer);
                rewindableReadonlyStream.ReturnTailOfReadedBytes(slices[0]);
                var readBuffer1 = new byte[slices[0].Count/2 + 3];
                var readBuffer2 = new byte[slices[0].Count/2 + 3];

                var read1 = rewindableReadonlyStream.Read(readBuffer1);
                var read2 = rewindableReadonlyStream.Read(readBuffer2);

                read1.Should().Be(readBuffer1.Length);
                read2.Should().Be(slices[0].Count - readBuffer1.Length);
                var readed = readBuffer1.Concat(readBuffer2.Slice(read2)).ToArray();
                readed.Should().BeEquivalentTo(slices[0].ToArray());
            }

            [Fact]
            public void Should_return_readed_bytes_and_allow_to_read_them_again()
            {
                var stream = TestData.InputFileContent.ToBytes().AsStream();
                var rewindableReadonlyStream = new RewindableReadonlyStream(stream);
                var buffer1 = new byte[stream.Length];
                var buffer2 = new byte[stream.Length];
                
                rewindableReadonlyStream.Read(buffer1);
                var slices = buffer1.Slices(buffer1.Length/2 + 1).ToArray();

                rewindableReadonlyStream.ReturnTailOfReadedBytes(slices[0]);
                rewindableReadonlyStream.ReturnTailOfReadedBytes(slices[1]);
                
                rewindableReadonlyStream.Read(buffer2);
                
                buffer1.Should().BeEquivalentTo(buffer2);
            }

            [Fact]
            public void Should_fail_when_try_to_return_too_much_bytes()
            {
                var stream = TestData.InputFileContent.ToBytes().AsStream();
                var rewindableReadonlyStream = new RewindableReadonlyStream(stream);
                var buffer = new byte[stream.Length];
                var slices = buffer.Slices(buffer.Length/2 + 1).ToArray();
                
                var beforeReturn = rewindableReadonlyStream.Position;

                Action action = () => rewindableReadonlyStream.ReturnTailOfReadedBytes(slices[1]);
                action.Should().Throw<ArgumentException>();
                
                var afterReturn = rewindableReadonlyStream.Position;
                
                beforeReturn.Should().Be(0);                
                afterReturn.Should().Be(0);                
            }
        }
    }
}