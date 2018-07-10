using System;

namespace Parallel.Compression.WorkersPool
{
    internal interface IWorkerTask : IDisposable
    {
        void Execute();
        string Id { get; }
    }
}