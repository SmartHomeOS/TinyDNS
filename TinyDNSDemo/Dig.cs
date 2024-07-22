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
using TinyDNS.Enums;
using TinyDNS.Records;
using TinyDNS;

namespace TinyDNSDemo
{
    internal class Dig
    {
        static bool running = true;
        static DNSResolver resolver = new DNSResolver(DNSSources.CloudflareDNSAddresses, ResolutionMode.SecureWithFallback);

        public static async Task Run()
        {
            try
            {
                PrintHelp();
                while (running)
                {
                    string? line = Console.ReadLine();
                    if (line == null || line == "exit")
                        Environment.Exit(0);
                    await ExecCommand(line);
                    Console.Write("\ndig ");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
            }
        }

        private static async Task ExecCommand(string line)
        {
            string[] cmds = line.Split(' ');
            bool multicast = false;
            bool inverse = false;
            string? domain = null;
            string? nameserver = null;
            IPAddress? address = null;
            DNSRecordType recordType = DNSRecordType.A;
            for (int i = 0; i < cmds.Length; i++)
            {
                if (cmds[i].StartsWith("-"))
                {
                    switch (cmds[i].Substring(1))
                    {
                        case "x":
                            inverse = true;
                            continue;
                        case "m":
                            multicast = true;
                            continue;
                        default:
                            Console.Error.WriteLine("Invalid Option " + cmds[i]);
                            return;
                    }
                }
                if ((cmds.Length - i) == 3)
                {
                    nameserver = cmds[i].Substring(1);
                    if (cmds[i + 1] == "-x")
                    {
                        inverse = true;
                        address = IPAddress.Parse(cmds[i + 2]);
                        break;
                    }
                    else
                    {
                        domain = cmds[i + 1];
                        if (!Enum.TryParse<DNSRecordType>(cmds[i + 2].ToUpper().Replace("*", "ANY"), out DNSRecordType rcd))
                        {
                            Console.Error.WriteLine("Invalid Record Type");
                            return;
                        }
                        recordType = rcd;
                    }
                    break;
                }
                else if ((cmds.Length - i) == 2)
                {
                    if (cmds[i].StartsWith("@"))
                    {
                        nameserver = cmds[i].Substring(1);
                        domain = cmds[i + 1];
                    }
                    else
                    {
                        domain = cmds[i];
                        if (!Enum.TryParse<DNSRecordType>(cmds[i + 1].ToUpper().Replace("*", "ANY"), out DNSRecordType rcd))
                        {
                            Console.Error.WriteLine("Invalid Record Type");
                            return;
                        }
                        recordType = rcd;
                    }
                    break;
                }
                else
                {
                    if (inverse)
                        address = IPAddress.Parse(cmds[i]);
                    else
                        domain = cmds[i];
                    break;
                }
            }
            try
            {
                if (multicast)
                {
                    MDNS mdns = new MDNS();
                    await mdns.Start();
                    List<Message> results;
                    if (inverse)
                    {
                        if (address == null)
                        {
                            Console.Error.WriteLine("Invalid Address");
                            return;
                        }
                        results = await mdns.ResolveInverseQuery(address);
                    }
                    else
                    {
                        if (domain == null)
                        {
                            Console.Error.WriteLine("Invalid Domain");
                            return;
                        }
                        results = await mdns.ResolveQuery(domain, recordType);
                    }
                    if (results.Count > 0)
                    {
                        Console.WriteLine($";; Received {results.Count} responses:");
                        foreach (var result in results)
                            Console.WriteLine(result.ToString());
                    }
                    else
                        Console.WriteLine(";; No answers received");
                }
                else
                {
                    if (inverse)
                    {
                        if (address == null)
                        {
                            Console.Error.WriteLine("Invalid Address");
                            return;
                        }
                        resolver.NameServers = DNSSources.CloudflareDNSAddresses;
                        if (nameserver != null)
                        {
                            if (!IPAddress.TryParse(nameserver, out IPAddress? ns))
                            {
                                var ips = await resolver.ResolveHost(nameserver);
                                if (ips.Count == 0)
                                {
                                    Console.Error.WriteLine($"Invalid Nameserver: {nameserver}");
                                    return;
                                }
                                resolver.NameServers = ips;
                            }
                            else
                                resolver.NameServers = [ns];
                        }
                        var result = await resolver.ResolveIPRecord(address);
                        if (result == null)
                            Console.WriteLine(";; No answers received");
                        else
                            Console.Write(result);
                    }
                    else
                    {
                        if (domain == null || domain.Length == 0)
                        {
                            Console.Error.WriteLine("Invalid Domain");
                            return;
                        }

                        resolver.NameServers = DNSSources.CloudflareDNSAddresses;
                        if (nameserver != null)
                        {
                            if (!IPAddress.TryParse(nameserver, out IPAddress? ns))
                            {
                                var ips = await resolver.ResolveHost(nameserver);
                                if (ips.Count == 0)
                                {
                                    Console.Error.WriteLine($"Invalid Nameserver: {nameserver}");
                                    return;
                                }
                                resolver.NameServers = ips;
                            }
                            else
                                resolver.NameServers = [ns];
                        }
                        QuestionRecord qr = new QuestionRecord(domain, recordType, false);
                        var response = await resolver.ResolveQuery(qr);
                        if (response == null)
                            Console.WriteLine(";; No answers received");
                        else
                            Console.Write(response);
                    }
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                running = false;
            }
        }

        private static void PrintHelp()
        {
            Version? version = AssemblyName.GetAssemblyName(@"TinyDNS.dll").Version;
            Console.WriteLine($"Tiny DIG v{version}\n");
            Console.WriteLine("Address Query: \t\t\tdig [domain]");
            Console.WriteLine("Address Query to Nameserver: \tdig @[nameserver] [domain]");
            Console.WriteLine("Specific Query: \t\tdig [domain] [record type]");
            Console.WriteLine("Query Specific Nameserver: \tdig @[nameserver] [domain] [record type]");
            Console.WriteLine("Inverse Query: \t\t\tdig -x [IP]\n");

            Console.WriteLine("Multicast Address Query: \tdig -m [domain]");
            Console.WriteLine("Multicast Specific Query: \tdig -m [domain] [record type]");
            Console.WriteLine("Multicast Inverse Query: \tdig -m -x [IP]\n");

            Console.WriteLine("End Program: \t\t\tdig exit");
            Console.Write("\ndig ");
        }
    }
}
