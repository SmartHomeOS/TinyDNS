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

using System.Net;
using System.Reflection;
using TinyDNS;

namespace TinyDNSDemo
{
    internal class EasyDNS
    {
        static DNSResolver resolver = new DNSResolver();
        public static async Task Run()
        {
            if (resolver.NameServers.Count == 0)
                resolver.NameServers = DNSSources.RootNameservers;
            try
            {
                PrintHelp();
                while (true)
                {
                    string? line = Console.ReadLine();
                    if (line == null || line == "3")
                        Environment.Exit(0);
                    await ExecCommand(line);
                    Console.Write("\nSelection: ");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
            }
        }

        private static async Task ExecCommand(string command)
        {
            string? domain;
            switch (command)
            {
                case "1": //Lookup IP
                    Console.Write("Enter IP: ");
                    if (!IPAddress.TryParse(Console.ReadLine(), out var address))
                    {
                        Console.Error.WriteLine("Invalid IP");
                        return;
                    }
                    domain = await resolver.ResolveIP(address);
                    if (domain == null)
                        Console.WriteLine("Domain not found");
                    else
                        Console.WriteLine($"{address} resolves to {domain}");
                    break;
                case "2": //Lookup Domain
                    Console.Write("Enter Domain: ");
                    domain = Console.ReadLine();
                    if (domain == null)
                        return;
                    List<IPAddress> addresses = await resolver.ResolveHost(domain);
                    if (addresses.Count == 0)
                        Console.WriteLine("IP not found");
                    else
                        Console.WriteLine($"{domain} resolves to {string.Join(", ", addresses)}");
                    break;
                default:
                    Console.WriteLine("Invalid Selection!");
                    return;
            }
        }

        private static void PrintHelp()
        {
            Version? version = AssemblyName.GetAssemblyName(@"TinyDNS.dll").Version;
            Console.WriteLine($"Easy DNS v{version}\n");
            Console.WriteLine("?) Print this help screen");
            Console.WriteLine("1) Lookup the domain from an IP"); 
            Console.WriteLine("2) Lookup the IP of a domain");
            Console.WriteLine("3) Exit");
            Console.Write("\nSelection: ");
        }
    }
}
