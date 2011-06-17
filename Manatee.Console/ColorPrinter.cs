using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Manatee.Command
{
    public class ColorPrinter : IDisposable
    {
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
