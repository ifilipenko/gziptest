using System;
using Parallel.Compression.Logging;
using Xunit.Abstractions;

namespace Parallel.Compression.Tests.Helpers
{
    internal class TestLog : ILog
    {
        private readonly ITestOutputHelper output;

        public TestLog(ITestOutputHelper output)
        {
            this.output = output;
        }

        public bool IsDebugEnabled { get; set; } = true;
        public bool IsInfoEnabled { get; } = true;

        public void Info(string message)
        {
            if (IsInfoEnabled)
                output.WriteLine(Timestamp + " [INFO] " + message);
        }

        public void Debug(string message)
        {
            if (IsDebugEnabled)
                output.WriteLine(Timestamp + " [DEBUG] " + message);
        }

        private string Timestamp => DateTime.Now.ToString("hh:mm:ss.fff");
    }
}