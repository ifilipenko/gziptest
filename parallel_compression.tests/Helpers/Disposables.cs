using System;
using System.Collections.Generic;

namespace Parallel.Compression.Tests.Helpers
{
    internal class Disposables : IDisposable
    {
        private readonly IReadOnlyList<IDisposable> disposables;

        public Disposables(IReadOnlyList<IDisposable> disposables)
        {
            this.disposables = disposables;
        }

        public void Dispose()
        {
            foreach (var disposable in disposables)
            {
                disposable?.Dispose();
            }
        }
    }
}