using System;
using System.Collections.Generic;
using System.Text;

namespace iBoxTelnetEnabler
{
    static class Utils
    {
        public static bool Confirm(string question)
        {
            Console.Write(question + " [y/n]: ");
            ConsoleKey k = Console.ReadKey().Key;
            Console.WriteLine();
            return (k == ConsoleKey.Y);
        }
    }
}
