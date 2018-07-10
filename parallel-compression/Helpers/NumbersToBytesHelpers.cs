namespace Parallel.Compression.Helpers
{
    public static class NumbersToBytesHelpers
    {
        public static long Kilobytes(this long value)
        {
            return value*1024;
        }

        public static long Megabytes(this long value)
        {
            return value*1024*1024;
        }

        public static int Kilobytes(this int value)
        {
            return value*1024;
        }

        public static int Megabytes(this int value)
        {
            return value*1024*1024;
        }
    }
}