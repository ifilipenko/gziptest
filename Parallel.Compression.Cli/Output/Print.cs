using System;

namespace Parallel.Compression.Cli.Output
{
    internal static class Print
    {
        public static void Info(string message)
        {
            Console.WriteLine(message);
        }

        public static void Error(string message)
        {
            ColorPrint(message, ConsoleColor.Red);
        }

        public static void Success(string message)
        {
            ColorPrint(message, ConsoleColor.Green);
        }

        private static void ColorPrint(string message, ConsoleColor color)
        {
            var oldColor = Console.ForegroundColor;
            try
            {
                Console.ForegroundColor = color;
                Console.WriteLine(message);
            }
            finally
            {
                Console.ForegroundColor = oldColor;
            }
        }
    }
}