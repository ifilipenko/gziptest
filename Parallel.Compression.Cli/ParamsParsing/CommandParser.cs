using System;
using System.Linq;
using JetBrains.Annotations;
using Parallel.Compression.Cli.Commands;
using Parallel.Compression.Cli.Output;
using Parallel.Compression.Configuration;
using Parallel.Compression.Func;

namespace Parallel.Compression.Cli.ParamsParsing
{
    internal static class CommandParser
    {
        public static Result<ICommand> Parse([NotNull] string[] args)
        {
            if (args == null)
                throw new ArgumentNullException(nameof(args));

            string command;
            if (args.Length == 0 || Commands.Help.IsMatched(command = args[0]))
            {
                return new HelpCommand();
            }

            if (Commands.Compress.IsMatched(command))
            {
                var (settings, error) = ParseCompressionCommand(Commands.Compress, args.Skip(1).ToArray());
                if (error != null)
                    return error;
                return new CompressCommand(
                    settings.InputFile,
                    settings.OutputFile,
                    settings.CompressionSettings);
            }

            if (Commands.Decompress.IsMatched(command))
            {
                var (settings, error) = ParseCompressionCommand(Commands.Decompress, args.Skip(1).ToArray());
                if (error != null)
                    return error;
                return new DecompressCommand(
                    settings.InputFile,
                    settings.OutputFile,
                    settings.CompressionSettings);
            }

            return new UnknownCommand();
        }

        private static Result<(
                string InputFile,
                string OutputFile,
                CompressorSettings CompressionSettings)>
            ParseCompressionCommand(
                CommandArgument command, string[] args)
        {
            var (commandArguments, parseError) = command.GetArguments(args);
            if (parseError != null)
                return parseError;
            
            var (parameters, error) = commandArguments.ToCompressionParameters();
            if (error != null)
                return error;

            var (settings, settingsError) = parameters.GetCompressorSettings();
            if (settingsError != null)
                return Messages.ParametersError(settingsError);

            return (parameters.InputFilePath, parameters.OutputFilePath, settings);
        }
    }
}