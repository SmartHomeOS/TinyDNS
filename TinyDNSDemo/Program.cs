//  TinyDNS Copyright (C) 2024
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published by
// the Free Software Foundation, either version 3 of the License, or any later version.
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY, without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU Affero General Public License for more details.
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using TinyDNSDemo;

internal class Program
{
    static async Task Main()
    {
        while (true)
        {
            PrintWelcome();
            string? cmd = Console.ReadLine();
            if (cmd == "1")
            {
                Console.Clear();
                await Dig.Run();
            }
            else if (cmd == "2")
            {
                Console.Clear();
                await EasyDNS.Run();
            }
            else if (cmd == "3")
            {
                Console.Clear();
                DNSSD browser = new DNSSD();
                await browser.Run();
                Console.ReadKey();
            }
            else if (cmd == "4")
                return;
            else
            {
                Console.WriteLine("Invalid Selection!");
                await Task.Delay(2000);
            }
        }
    }

    private static void PrintWelcome()
    {
        Console.Clear();
        Console.WriteLine("Welcome to the TinyDNS Demo");
        Console.WriteLine("Press 1 to launch Tiny DIG");
        Console.WriteLine("Press 2 to launch Easy DNS");
        Console.WriteLine("Press 3 to launch DNS-SD Browser");
        Console.WriteLine("Press 4 to exit\n");
        Console.Write("Selection: ");
    }
}