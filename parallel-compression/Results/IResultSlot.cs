namespace Parallel.Compression.Results
{
    internal interface IResultSlot<in TResult>
    {
        void SetResult(TResult result);
    }
}