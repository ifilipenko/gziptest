using System;
using JetBrains.Annotations;

namespace Parallel.Compression.WorkersPool
{
    internal interface IWorkersPool : IDisposable
    {
        int TasksQueueSize { get; }
        int TaskInProgress { get; }
        void PushTask([NotNull] IWorkerTask task);
    }
}