using System;
using System.IO;
using JetBrains.Annotations;
using Parallel.Compression.Errors;
using Parallel.Compression.Func;

namespace Parallel.Compression.IO
{
    internal class InputFile
    {
        private readonly Stream stream;

        public InputFile([NotNull] Stream stream)
        {
            this.stream = stream ?? throw new ArgumentNullException(nameof(stream));

            if (!stream.CanSeek)
                throw new ArgumentException("Stream should be able to seek");
            if (!stream.CanRead)
                throw new ArgumentException("Stream should be able to read");
        }

        public Result<int, ErrorCodes?> ReadTo([NotNull] byte[] buffer)
        {
            if (buffer == null) 
                throw new ArgumentNullException(nameof(buffer));
            if (buffer.Length == 0) 
                throw new ArgumentException("Buffer can't be empty", nameof(buffer));

            try
            {
                return stream.Read(buffer, 0, buffer.Length);
            }
            catch (DirectoryNotFoundException)
            {
                return ErrorCodes.InputFileDirectoryNotFound;
            }
            catch (FileNotFoundException)
            {
                return ErrorCodes.InputFileNotFound;
            }
            catch (PathTooLongException)
            {
                return ErrorCodes.InputFilePathTooLong;
            }
            catch (UnauthorizedAccessException)
            {
                return ErrorCodes.InputFileReadUnauthorized;
            }
        }

        public ErrorCodes? SeekTo(long offset)
        {
            try
            {
                stream.Seek(offset, SeekOrigin.Begin);
            }
            catch (DirectoryNotFoundException)
            {
                return ErrorCodes.InputFileDirectoryNotFound;
            }
            catch (FileNotFoundException)
            {
                return ErrorCodes.InputFileNotFound;
            }
            catch (PathTooLongException)
            {
                return ErrorCodes.InputFilePathTooLong;
            }
            catch (UnauthorizedAccessException)
            {
                return ErrorCodes.InputFileReadUnauthorized;
            }

            return null;
        }
    }
}