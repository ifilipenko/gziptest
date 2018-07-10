namespace Parallel.Compression.Logging
{
    internal static class LogHelpers
    {
        public static ILog WithPrefix(this ILog log, string prefix)
        {
            return new PrefixedLog(log, prefix);
        }

        private class PrefixedLog : ILog
        {
            private readonly ILog log;
            private readonly string prefix;

            public PrefixedLog(ILog log, string prefix)
            {
                this.log = log;
                this.prefix = prefix + " ";
            }

            public bool IsInfoEnabled => log.IsInfoEnabled;

            public bool IsDebugEnabled => log.IsDebugEnabled;

            public void Info(string message)
            {
                log.Info(prefix + message);
            }

            public void Debug(string message)
            {
                log.Debug(prefix + message);
            }
        }
    }
}