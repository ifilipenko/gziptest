using System.Collections.Generic;
using Parallel.Compression.Func;

namespace Parallel.Compression.Cli.ParamsParsing
{
    internal class CommandArgumentValues
    {
        private readonly Dictionary<string, string> values;

        public CommandArgumentValues(Dictionary<string, string> values)
        {
            this.values = values;
        }

        public string this[string name]
        {
            get
            {
                if (values.TryGetValue(name, out var value))
                {
                    return value;
                }

                throw new KeyNotFoundException($"Required parameter \"{name}\" is not found");
            }
        }

        public Result<int?> GetOptionAsInt(CommandKeyOption option)
        {
            if ((option.ShortKey != null && values.TryGetValue(option.ShortKey, out var value)) ||
                (option.LongKey != null && values.TryGetValue(option.LongKey, out value)))
            {
                return !int.TryParse(value, out var result)
                    ? (Result<int?>) $"{option.Description} should be positive number"
                    : result;
            }

            return (int?) null;
        }
    }
}