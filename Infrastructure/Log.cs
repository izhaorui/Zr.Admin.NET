using System;

namespace Infrastructure
{
    public class Log
    {
        public static void WriteLine(ConsoleColor color = ConsoleColor.Black, string msg = "")
        {
            Console.ForegroundColor = color;
            Console.WriteLine($"{DateTime.Now} {msg}");
            Console.ResetColor();
        }
    }
}
