using System;

namespace Parallel.Compression.TaskQueue
{
    internal interface ITask : IDisposable
    {
        string Id { get; }
        object Execute();
    }
}