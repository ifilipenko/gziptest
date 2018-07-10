using System;
using System.IO;
using System.Text;
using JetBrains.Annotations;
using Parallel.Compression.Errors;
using Parallel.Compression.Func;
using Parallel.Compression.Helpers;

namespace Parallel.Compression.IO
{
    internal class OutputFile
    {
        private enum OffsetReadingErrors
        {
            FileTooShort,
            NotFoundOffsetSection,
            FileReducedWhileReadingOffsetSection
        }

        private const int LongBytesLength = 8;

        private readonly Stream outputStream;
        private readonly byte[] offsetLabel;
        private readonly int offsetSectionLength;
        private readonly byte[] offsetSectionBuffer;

        public OutputFile([NotNull] Stream outputStream, string offsetLabel)
        {
            if (string.IsNullOrWhiteSpace(offsetLabel))
                throw new ArgumentException("Trailing offset label can't be null or whitespace", nameof(offsetLabel));

            if (outputStream == null)
                throw new ArgumentNullException(nameof(outputStream));
            if (!outputStream.CanWrite)
                throw new ArgumentException("Stream should be writeable", nameof(outputStream));
            if (!outputStream.CanRead)
                throw new ArgumentException("Stream should be readable", nameof(outputStream));
            if (!outputStream.CanSeek)
                throw new ArgumentException("Stream should be seekable", nameof(outputStream));

            this.outputStream = outputStream;
            this.offsetLabel = Encoding.ASCII.GetBytes(offsetLabel);
            offsetSectionLength = offsetLabel.Length + LongBytesLength;
            offsetSectionBuffer = new byte[offsetSectionLength];
        }

        public ErrorCodes? Append(ArraySegment<byte> bytes, long offset)
        {
            if (offset < 0)
                throw new ArgumentException("Offset should be positive integer", nameof(offset));

            try
            {
                if (outputStream.Length > 0)
                {
                    var offsetOverwringError = OverwritePreviousOffset();
                    if (offsetOverwringError.HasValue)
                        return offsetOverwringError;

                    outputStream.Seek(-offsetSectionLength, SeekOrigin.End);
                }

                WriteBlock();
                WriteOffsetSection();

                return null;
            }
            catch (DirectoryNotFoundException)
            {
                return ErrorCodes.OutputFileDirectoryNotFound;
            }
            catch (FileNotFoundException)
            {
                return ErrorCodes.OutputFileNotFound;
            }
            catch (PathTooLongException)
            {
                return ErrorCodes.OutputFilePathTooLong;
            }
            catch (UnauthorizedAccessException)
            {
                return ErrorCodes.OutputFileReadUnauthorized;
            }

            ErrorCodes? OverwritePreviousOffset()
            {
                var (_, offsetReadingError) = ReadOffsetFromTheOfTheStream();
                return offsetReadingError.HasValue ? ToErrorCode(offsetReadingError.Value) : null;
            }

            void WriteBlock()
            {
                if (bytes.Count > 0)
                {
                    outputStream.Write(bytes.Array, bytes.Offset, bytes.Count);
                }
            }

            void WriteOffsetSection()
            {
                outputStream.Write(offsetLabel, 0, offsetLabel.Length);

                var longBytes = BitConverter.GetBytes(offset);
                outputStream.Write(longBytes, 0, longBytes.Length);
            }
        }

        public Result<long, ErrorCodes?> GetLastOffset()
        {
            try
            {
                var (offset, offsetReadingError) = ReadOffsetFromTheOfTheStream();
                if (offsetReadingError.HasValue)
                {
                    if (outputStream.Length == 0)
                        return 0;
                    return ToErrorCode(offsetReadingError.Value);
                }

                return offset;
            }
            catch (DirectoryNotFoundException)
            {
                return ErrorCodes.OutputFileDirectoryNotFound;
            }
            catch (FileNotFoundException)
            {
                return ErrorCodes.OutputFileNotFound;
            }
            catch (PathTooLongException)
            {
                return ErrorCodes.OutputFilePathTooLong;
            }
            catch (UnauthorizedAccessException)
            {
                return ErrorCodes.OutputFileReadUnauthorized;
            }
        }

        public ErrorCodes? Commit()
        {
            try
            {
                if (outputStream.Length < offsetSectionLength)
                {
                    return ErrorCodes.OutputFileHaveWrongFormatOrAlreadyCommitted;
                }

                var (_, offsetReadingError) = ReadOffsetFromTheOfTheStream();
                if (offsetReadingError.HasValue)
                {
                    return ToErrorCode(offsetReadingError.Value);
                }

                outputStream.SetLength(outputStream.Length - offsetSectionLength);

                return null;
            }
            catch (DirectoryNotFoundException)
            {
                return ErrorCodes.OutputFileDirectoryNotFound;
            }
            catch (FileNotFoundException)
            {
                return ErrorCodes.OutputFileNotFound;
            }
            catch (PathTooLongException)
            {
                return ErrorCodes.OutputFilePathTooLong;
            }
            catch (UnauthorizedAccessException)
            {
                return ErrorCodes.OutputFileReadUnauthorized;
            }
        }
        
        public int CompressionRatio(long inputFileLength)
        {
            return (int) Math.Floor(outputStream.Length * 100.0/inputFileLength);
        }

        private ErrorCodes? ToErrorCode(OffsetReadingErrors value)
        {
            switch (value)
            {
                case OffsetReadingErrors.NotFoundOffsetSection:
                case OffsetReadingErrors.FileTooShort:
                    return ErrorCodes.OutputFileHaveWrongFormatOrAlreadyCommitted;
                case OffsetReadingErrors.FileReducedWhileReadingOffsetSection:
                    return ErrorCodes.OutputFileUnexpectedlyReduced;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value), value, null);
            }
        }

        private Result<long, OffsetReadingErrors?> ReadOffsetFromTheOfTheStream()
        {
            if (outputStream.Length < offsetSectionLength)
            {
                return OffsetReadingErrors.FileTooShort;
            }

            outputStream.Seek(-offsetSectionLength, SeekOrigin.End);

            var result = outputStream.ReadExactFullBuffer(offsetSectionBuffer);
            if (result.IsFailed)
                return OffsetReadingErrors.FileReducedWhileReadingOffsetSection;

            if (!offsetSectionBuffer.Slice(offsetLabel.Length).UnsafeEquals(offsetLabel))
            {
                return OffsetReadingErrors.NotFoundOffsetSection;
            }

            var offsetValueBytes = offsetSectionBuffer.SliceFromTheEnd(8);
            return BitConverter.ToInt64(offsetValueBytes.Array, offsetValueBytes.Offset);
        }
    }
}