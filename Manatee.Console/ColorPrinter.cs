using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Manatee.Command
{
    public class ColorPrinter : IDisposable
    {
        public static ColorPrinter Red
        {
            get { return new ColorPrinter(ConsoleColor.Red); }
        }

        public static ColorPrinter Yellow
        {
            get { return new ColorPrinter(ConsoleColor.Yellow); }
        }

        public static ColorPrinter Green
        {
            get { return new ColorPrinter(ConsoleColor.Green); }
        }

        public ColorPrinter(ConsoleColor color)
        {
            PreviousColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
        }
        
        protected ConsoleColor PreviousColor { get; set; }
        public void Dispose()
        {
            Console.ForegroundColor = PreviousColor;
        }
    }
}
