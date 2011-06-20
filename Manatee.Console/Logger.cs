using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Manatee.Command
{
    public static class Logger
    {
        public static void WriteLine(string format, params object[] args)
        {
            Console.WriteLine(format, args);
        }

        public static void WriteLine(ConsoleColor color, string format, params object[] args)
        {
            using(new ColorPrinter(color))
                WriteLine(format, args);
        }

        public static void Write(ConsoleColor color, string format, params object[] args)
        {
            using(new ColorPrinter(color))
                Write(format, args);
        }

        public static void Write(string format, params object[] args)
        {
            Console.Write(format, args);
        }
    }
}
