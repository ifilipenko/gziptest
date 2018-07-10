using System.Collections.Generic;
using JetBrains.Annotations;
using Parallel.Compression.Decompression.Streams;

namespace Parallel.Compression.Decompression.GzipSplitting
{
    internal interface IGzipToIndependentBlockSplitter
    {
        IEnumerable<SplitResult> SplitToIndependentBlocks([NotNull] RewindableReadonlyStream inputStream);
    }
}