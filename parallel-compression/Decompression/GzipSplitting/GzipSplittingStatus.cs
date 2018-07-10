namespace Parallel.Compression.Decompression.GzipSplitting
{
    internal enum GzipSplittingStatus
    {
        StreamIsEnd,
        WrongFormat,
        CantReadBlock,
        Block
    }
}