using System;

namespace Parallel.Compression.Cli.ParamsParsing
{
    internal class CommandKeyOption
    {
        public static bool ValueLooksLikeOptionKey(string value)
        {
            return value?.StartsWith("-") == true;
        }

        public CommandKeyOption(string shortKey, string longKey, string description)
        {
            ShortKey = shortKey;
            LongKey = longKey;
            Description = description;
        }

        public string ShortKey { get; }
        public string LongKey { get; }
        public string Description { get; }

        public string Signature
        {
            get
            {
                string key;
                if (ShortKey != null && LongKey != null)
                {
                    key = ShortKey + "|" + LongKey;
                }
                else if (ShortKey == null)
                {
                    key = LongKey;
                }
                else
                {
                    key = ShortKey;
                }

                return $"{key}";
            }
        }

        public bool IsMatched(string key)
        {
            return ShortKey?.Equals(key, StringComparison.OrdinalIgnoreCase) == true ||
                   LongKey?.Equals(key, StringComparison.OrdinalIgnoreCase) == true;
        }
    }
}