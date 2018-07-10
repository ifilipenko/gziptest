namespace Parallel.Compression.Logging
{
    public class StubLog : ILog
    {
        public bool IsDebugEnabled { get; } = false;
        public bool IsInfoEnabled { get; } = false;
        
        public void Info(string message)
        {
        }

        public void Debug(string message)
        {
        }
    }
}