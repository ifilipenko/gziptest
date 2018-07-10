using System;

namespace Parallel.Compression.WorkersPool
{
    internal class DelegateWorkerTask : IWorkerTask
    {
        private readonly Action action;

        public DelegateWorkerTask(string id, Action action)
        {
            Id = id;
            this.action = action;
        }

        public string Id { get; }
        
        public void Execute()
        {
            action();
        }
        
        public void Dispose()
        {
        }
    }
}