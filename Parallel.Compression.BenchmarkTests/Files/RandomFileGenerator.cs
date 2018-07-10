using System;
using System.Diagnostics;
using System.IO;

namespace Parallel.Compression.BenchmarkTests.Files
{
    internal class RandomFileGenerator
    {
        private static readonly char[] Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789".ToCharArray();
        private readonly Random random = new Random();

        public void GenerateTextFile(string fileName, int size)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            using (var stream = File.OpenWrite(fileName))
            using (var streamWriter = new StreamWriter(stream))
            {
                for (var i = 0; i < size; i++)
                {
                    var randChar = Chars[random.Next(0, Chars.Length)];
                    streamWriter.Write(randChar);
                }
            }

            stopwatch.Stop();
            Console.WriteLine($"File {fileName} with size {size} generated at {stopwatch.Elapsed}");
        }
    }
}