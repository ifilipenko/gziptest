namespace Parallel.Compression.Errors
{
    public enum ErrorCodes
    {
        InputFileNotFound,
        InputFileDirectoryNotFound,
        InputFilePathTooLong,
        InputFileReadUnauthorized,
        InputFilePathContainInvalidPathChars,
        InputFilePathHaveUnsupporterFormat,
        InputFileDriveNotFound,

        OutputFileUnexpectedlyReduced,
        OutputFileHaveWrongFormatOrAlreadyCommitted,

        OutputFileNotFound,
        OutputFileDirectoryNotFound,
        OutputFilePathTooLong,
        OutputFileReadUnauthorized,
        OutputFileDriveNotFound,
        OutputFilePathHaveUnsupporterFormat,
        OutputFilePathContainInvalidPathChars,

        NothingToCompress,
        NothingToDecompress,
        
        CompressedFileIsNotGzip,
        DecompressionBufferIsTooSmall
    }
}