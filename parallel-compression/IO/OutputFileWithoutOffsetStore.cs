using System;
using System.IO;
using System.Text;
using JetBrains.Annotations;
using Parallel.Compression.Errors;
using Parallel.Compression.Func;
using Parallel.Compression.Helpers;

namespace Parallel.Compression.IO
{
    internal class OutputFileWithoutOffsetStore
    {
        private enum OffsetReadingErrors
        {
            FileTooShort,
            NotFoundOffsetSection,
            FileReducedWhileReadingOffsetSection
        }

        private const int LongBytesLength = 8;
        private readonly Stream outputStream;

        public OutputFileWithoutOffsetStore([NotNull] Stream outputStream)
        {
            if (outputStream == null)
                throw new ArgumentNullException(nameof(outputStream));
            if (!outputStream.CanWrite)
                throw new ArgumentException("Stream should be writeable", nameof(outputStream));
            if (!outputStream.CanRead)
                throw new ArgumentException("Stream should be readable", nameof(outputStream));
            if (!outputStream.CanSeek)
                throw new ArgumentException("Stream should be seekable", nameof(outputStream));

            this.outputStream = outputStream;
        }

        public ErrorCodes? Append(ArraySegment<byte> bytes)
        {
            try
            {
                WriteBlock();
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

            void WriteBlock()
            {
                if (bytes.Array != null && bytes.Count > 0)
                {
                    outputStream.Write(bytes.Array, bytes.Offset, bytes.Count);
                }
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
    }
}