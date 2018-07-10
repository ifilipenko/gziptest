using System;
using System.Collections.Generic;
using System.Linq;
using Parallel.Compression.Func;

namespace Parallel.Compression.Cli.ParamsParsing
{
    internal class CommandArgument
    {
        private readonly List<(string key, string description)> positionParameters =
            new List<(string key, string description)>();

        private readonly List<CommandKeyOption> options = new List<CommandKeyOption>();

        public CommandArgument(string key, string description)
        {
            Key = key;
            Description = description;
        }

        public string Key { get; }
        public string Description { get; }
        public string Signature => $"{Key} " + string.Join(" ", positionParameters.Select(x => "<" + x.key + ">"));

        public IEnumerable<(string Key, string Description)> ParametersDescriptions()
        {
            return positionParameters
                .Select(x => (x.key, x.description))
                .Concat(options.Select(op => (op.Signature, op.Description)));
        }

        public CommandArgument AddPositionParameters(string key, string description)
        {
            positionParameters.Add((key, description));
            return this;
        }

        public CommandArgument AddOption(CommandKeyOption option)
        {
            options.Add(option);
            return this;
        }

        public bool IsMatched(string value)
        {
            return Key.Equals(value, StringComparison.OrdinalIgnoreCase);
        }

        public Result<CommandArgumentValues> GetArguments(string[] args)
        {
            var requiredParametersCount = positionParameters.Count;
            if (args.Length < requiredParametersCount)
                return $"For command {Key} expected minimum {requiredParametersCount} parameters but was {args.Length}";

            var optionalParamsCount = args.Length - requiredParametersCount;
            if (optionalParamsCount%2 != 0)
                return "Invalid parameters count";

            var result = new Dictionary<string, string>();

            for (var i = 0; i < requiredParametersCount; i++)
            {
                var value = args[i];
                var parameter = positionParameters[i];
                if (string.IsNullOrWhiteSpace(value) || CommandKeyOption.ValueLooksLikeOptionKey(value))
                {
                    return "Required parameter is missing: " + parameter.description;
                }

                result[parameter.key] = value;
            }

            for (var i = requiredParametersCount; i < args.Length; i += 2)
            {
                var key = args[i];
                if (options.Exists(o => o.IsMatched(key)))
                {
                    result[key] = args[i + 1];
                }
            }

            return new CommandArgumentValues(result);
        }
    }
}