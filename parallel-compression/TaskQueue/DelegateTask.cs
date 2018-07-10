using System;

namespace Parallel.Compression.TaskQueue
{
    internal class DelegateTask : ITask
    {
        private readonly Func<object> resultFactory;

        public DelegateTask(string id, Func<object> resultFactory)
        {
            this.resultFactory = resultFactory;
            Id = id;
        }

        public string Id { get; }

        public object Execute()
        {
            return resultFactory();
        }

        public void Dispose()
        {
        }
    }
}