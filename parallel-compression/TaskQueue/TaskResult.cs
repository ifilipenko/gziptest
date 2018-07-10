using System;

namespace Parallel.Compression.TaskQueue
{
    internal struct TaskResult
    {
        public TaskResult(object result, Exception exception)
        {
            Result = result;
            Exception = exception;
        }

        public object Result { get; }
        public Exception Exception { get; }
        public bool IsFailed => Exception != null;
    }
}