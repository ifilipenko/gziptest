using Parallel.Compression.Models;

namespace Parallel.Compression.Compression
{
    public interface IBlockCompression
    {
        Block Compress(Block inputBlock, DecompressionHelpMode decompressionHelpMode = DecompressionHelpMode.BlockLengthInMimetypeSection);
    }
}