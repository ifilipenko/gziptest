namespace Parallel.Compression.Logging
{
    public interface ILog
    {
        bool IsDebugEnabled { get; }
        bool IsInfoEnabled { get; }
        void Info(string message);
        void Debug(string message);
    }
}