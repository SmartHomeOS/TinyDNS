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
using TinyDNS;
using TinyDNS.Enums;

internal class Program
{
    static async Task Main()
    {
        DNSResolver resolver = new DNSResolver(DNSSources.CloudflareDNSAddresses, ResolutionMode.SecureWithFallback); //From root hints
        string host = "google.com";
        List<IPAddress> ip = await resolver.ResolveHost(host);
        if (ip.Count > 0)
            Console.WriteLine($"Resolved {host} as {ip[0]}");
        List<IPAddress> ip2 = await resolver.ResolveHostV6("mail." + host);
        if (ip2.Count == 0)
            Console.WriteLine("Unable to resolve IPs");
        else
        {
            Console.WriteLine($"Resolved mail.{host} as {ip2[0]}");
        }
        Console.ReadLine();
    }
}