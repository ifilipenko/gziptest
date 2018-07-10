using System;

namespace Parallel.Compression.Decompression.GzipSplitting
{
    internal struct SplitResult
    {
        public static SplitResult WithBlock(IndependentGzipBlock block)
        {
            return new SplitResult
            {
                Block = block,
                Status = GzipSplittingStatus.Block
            };
        }

        public static SplitResult StatusOnly(GzipSplittingStatus status)
        {
            if (status == GzipSplittingStatus.Block)
                throw new ArgumentException("Block status sets only with block");
            return new SplitResult
            {
                Status = status
            };
        }

        public IndependentGzipBlock Block { get; private set; }
        public GzipSplittingStatus Status { get; private set; }
        public bool HasBlock => Status == GzipSplittingStatus.Block;
        public bool HasOnlyStatus => !HasBlock;

        public void Deconstruct(out IndependentGzipBlock block, out GzipSplittingStatus status)
        {
            block = Block;
            status = Status;
        }

        public static implicit operator SplitResult(IndependentGzipBlock block)
        {
            return WithBlock(block);
        }

        public static implicit operator SplitResult(GzipSplittingStatus status)
        {
            return StatusOnly(status);
        }
    }
}