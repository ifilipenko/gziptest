using System;

namespace Parallel.Compression.Logging
{
    public class ConsoleLog : ILog
    {
        public bool IsInfoEnabled { get; } = true;
        public bool IsDebugEnabled { get; } = true;

        public void Info(string message)
        {
            if (IsInfoEnabled)
                Console.WriteLine("[INFO] " + message);
        }

        public void Debug(string message)
        {
            if (IsDebugEnabled)
                Console.WriteLine("[DEBUG] " + message);
        }
    }
}