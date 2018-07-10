using System;
using System.IO;
using Parallel.Compression.Errors;
using Parallel.Compression.Func;

namespace Parallel.Compression.IO
{
    public class FileSystem : IFileSystem
    {
        public Result<Stream, ErrorCodes?> OpenFileToRead(string inputFilePath)
        {
            if (string.IsNullOrWhiteSpace(inputFilePath))
                throw new ArgumentException("File path can't be empty", nameof(inputFilePath));

            try
            {
                return File.OpenRead(inputFilePath);
            }
            catch (ArgumentException)
            {
                return ErrorCodes.InputFilePathContainInvalidPathChars;
            }
            catch (NotSupportedException)
            {
                return ErrorCodes.InputFilePathHaveUnsupporterFormat;
            }
            catch (DriveNotFoundException)
            {
                return ErrorCodes.InputFileDriveNotFound;
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

        public Result<Stream, ErrorCodes?> OpenFileToReadWrite(string inputFilePath)
        {
            if (string.IsNullOrWhiteSpace(inputFilePath))
                throw new ArgumentException("File path can't be empty", nameof(inputFilePath));

            try
            {
                return File.Open(inputFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            }
            catch (ArgumentException)
            {
                return ErrorCodes.OutputFilePathContainInvalidPathChars;
            }
            catch (NotSupportedException)
            {
                return ErrorCodes.OutputFilePathHaveUnsupporterFormat;
            }
            catch (DriveNotFoundException)
            {
                return ErrorCodes.OutputFileDriveNotFound;
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
    }
}