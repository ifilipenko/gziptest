using System;
using System.Text;
using Parallel.Compression.Configuration;
using Parallel.Compression.Errors;
using static System.Environment;

namespace Parallel.Compression.Cli.Output
{
    internal static class Messages
    {
        public static string ParametersError(string error)
        {
            return "Invalid arguments: " + error;
        }

        public static string Error(ErrorCodes error)
        {
            switch (error)
            {
                case ErrorCodes.InputFileNotFound:
                    return "Input file not found";
                case ErrorCodes.InputFileDirectoryNotFound:
                    return "Input file directory not found";
                case ErrorCodes.InputFilePathTooLong:
                    return "Input file path not found";
                case ErrorCodes.InputFileReadUnauthorized:
                    return "Input file unauhorized to read";
                case ErrorCodes.InputFilePathContainInvalidPathChars:
                    return "Invalid input file path";
                case ErrorCodes.InputFilePathHaveUnsupporterFormat:
                    return "Input file path format is unsupported";
                case ErrorCodes.InputFileDriveNotFound:
                    return "Input file drive not found";
                case ErrorCodes.OutputFileUnexpectedlyReduced:
                    return "Output file reduced unexpectedly between writes compressed data";
                case ErrorCodes.OutputFileHaveWrongFormatOrAlreadyCommitted:
                    return "Output file have wrong format or already commited";
                case ErrorCodes.OutputFileNotFound:
                    return "Output file not found";
                case ErrorCodes.OutputFileDirectoryNotFound:
                    return "Output file directory not found";
                case ErrorCodes.OutputFilePathTooLong:
                    return "Output file path not found";
                case ErrorCodes.OutputFileReadUnauthorized:
                    return "Output file unauhorized to read or write";
                case ErrorCodes.OutputFileDriveNotFound:
                    return "Output file drive not found";
                case ErrorCodes.OutputFilePathHaveUnsupporterFormat:
                    return "Output file path format is unsupported";
                case ErrorCodes.OutputFilePathContainInvalidPathChars:
                    return "Invalid output file path";
                case ErrorCodes.NothingToCompress:
                    return "Input file is too short";
                case ErrorCodes.NothingToDecompress:
                    return "Input file is too short";
                case ErrorCodes.CompressedFileIsNotGzip:
                    return "Wrong gzip format";
                case ErrorCodes.DecompressionBufferIsTooSmall:
                    return "To descompress file need buffer with greater size";
                default:
                    throw new ArgumentOutOfRangeException(nameof(error), error, null);
            }
        }
        
        public static string UnexpectedException(Exception exception)
        {
            return "Unexpected exception occurence " + exception;
        }

        public static string Parameters(CompressorSettings settings)
        {
            return "Threads count: " + settings.ThreadsCount + NewLine +
                   "CPU cores: " + ProcessorCount + NewLine +
                   "Reading buffer size: " + settings.InputFileReadingBufferSize + NewLine +
                   "Compression queue size: " + settings.CompressingQueueSize;
        }
        
        public static string SuccessfulCompress(int ratio, TimeSpan elapsed)
        {
            return $"Compression complete with {ratio}% compression rate for {elapsed} time";
        }
        
        public static string SuccessfulDecompress(int ratio, TimeSpan elapsed)
        {
            return $"Decompression complete with {ratio}% compression rate for {elapsed} time";
        }
        
        public static string Help()
        {
            var text = new StringBuilder();
            text.AppendLine("Commands:").AppendLine();
            foreach (var command in ParamsParsing.Commands.All)
            {
                text.AppendLine($"  {command.Signature}\t{command.Description}");
                foreach (var parameter in command.ParametersDescriptions())
                {
                    text.AppendLine($"\t\t{parameter.Key}\t\t{parameter.Description}");                    
                }

                text.AppendLine();
            }

            return text.ToString();
        }

        public const string UnknownCommand = "Unknown command!";
    }
}