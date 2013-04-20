using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;

namespace iBoxTelnetEnabler
{
    class Program
    {

        static void Main(string[] args)
        {
            Console.Title = "iBoxTelnetEnabler";
            iBox boksi = iBox.find();
            if (boksi == null)
            {

                Console.WriteLine("No iBox was found in your network");
                Thread.Sleep(200);
                Console.WriteLine("Quitting...");
                Thread.Sleep(1000);
                return;
            }
            Thread.Sleep(500);

            while (true)
            {
                boksi.interact();

                if (Utils.Confirm("Quit?"))
                    return;
            }
        }
    }
}
