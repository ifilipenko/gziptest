using System;
using Parallel.Compression.TaskQueue;

namespace Parallel.Compression.Tests.Helpers
{
    internal class DelegateTask : ITask
    {
        private readonly Func<object> resultFactory;

        public DelegateTask(string title, Func<object> resultFactory)
        {
            this.resultFactory = resultFactory;
            Id = title;
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