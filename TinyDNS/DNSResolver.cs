// TinyDNS Copyright (C) 2024 
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
using System.Net.NetworkInformation;
using System.Net.Sockets;
using TinyDNS.Cache;
using TinyDNS.Enums;
using TinyDNS.Records;

namespace TinyDNS
{
    public sealed class DNSResolver
    {
        public const int PORT = 53;
        private readonly HashSet<IPAddress> globalNameservers = [];
        private ResolverCache cache = new ResolverCache();
        public DNSResolver()
        {
            ReloadNameservers();
            NetworkChange.NetworkAddressChanged += (s, e) => ReloadNameservers();
        }

        public DNSResolver(List<IPAddress> nameservers)
        {
            foreach (IPAddress nameserver in nameservers)
                this.globalNameservers.Add(nameserver);
        }

        public List<IPAddress> NameServers { get { return globalNameservers.ToList(); } }

        private void ReloadNameservers()
        {
            globalNameservers.Clear();
            var nics = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var nic in nics)
            {
                if (nic.OperationalStatus == OperationalStatus.Up && !nic.IsReceiveOnly && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                {
                    foreach (IPAddress ns in nic.GetIPProperties().DnsAddresses)
                        globalNameservers.Add(ns);
                }
            }
        }

        public async Task<List<IPAddress>> ResolveHost(string hostname)
        {
            List<IPAddress> addresses = 
                [
                    .. await ResolveHostV4(hostname),
                    .. await ResolveHostV6(hostname)
                ];
            return addresses;
        }

        public async Task<List<IPAddress>> ResolveHostV4(string hostname)
        {
            List<IPAddress> addresses = new List<IPAddress>();
            Message? response = await ResolveQuery(new QuestionRecord(hostname, DNSRecordType.A, false));
            if (response == null || response.ResponseCode != DNSStatus.OK || (response.Answers.Length == 0 && response.Additionals.Length == 0))
                return addresses;

            foreach (ResourceRecord answer in response.Answers)
            {
                if (answer is ARecord A)
                    addresses.Add(A.Address);
            }
            foreach (ResourceRecord additional in response.Additionals)
            {
                if (additional is ARecord A && A.Name == hostname)
                    addresses.Add(A.Address);
            }
            return addresses;
        }

        public async Task<List<IPAddress>> ResolveHostV6(string hostname)
        {
            List<IPAddress> addresses = new List<IPAddress>();
            Message? response = await ResolveQuery(new QuestionRecord(hostname, DNSRecordType.AAAA, false));
            if (response == null || response.ResponseCode != DNSStatus.OK || (response.Answers.Length == 0 && response.Additionals.Length == 0))
                return addresses;

            foreach (ResourceRecord answer in response.Answers)
            {
                if (answer is AAAARecord AAAA)
                    addresses.Add(AAAA.Address);
            }
            foreach (ResourceRecord additional in response.Additionals)
            {
                if (additional is AAAARecord AAAA && AAAA.Name == hostname)
                    addresses.Add(AAAA.Address);
            }
            return addresses;
        }

        public async Task<string?> ResolveIP(IPAddress address)
        {
            byte[] addressBytes = address.GetAddressBytes();
            List<string> host;
            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                host = new List<string>(6);
                for (int i = addressBytes.Length - 1; i >= 0; i--)
                    host.Add(addressBytes[i].ToString());

                host.Add("in-addr");
                host.Add("arpa");
            }
            else
            {
                host = new List<string>(34);
                for (int i = addressBytes.Length - 1; i >= 0; i--)
                {
                    string hex = addressBytes[i].ToString("x2");
                    host.Add(hex.Substring(1,1));
                    host.Add(hex.Substring(0, 1));
                }
                host.Add("IP6");
                host.Add("ARPA");
            }
            Message? response = await ResolveQuery(new QuestionRecord(host, DNSRecordType.PTR, false));
            if (response == null || response.ResponseCode != DNSStatus.OK)
                return null;

            foreach (ResourceRecord answer in response.Answers)
            {
                if (answer is PtrRecord ptr)
                    return ptr.Domain;
            }
            return null;
        }

        public async Task<Message?> ResolveQuery(QuestionRecord question)
        {
            return await ResolveQueryInternal(question, globalNameservers);
        }

        private async Task<Message?> ResolveQueryInternal(QuestionRecord question, HashSet<IPAddress> nameservers, int recursionCount = 0)
        {
            recursionCount++;
            if (recursionCount > 10)
                return null;

            ResourceRecord[]? cacheHits = cache.Search(question);
            if (cacheHits != null && cacheHits.Length > 0)
            {
                Message msg = new Message();
                msg.Response = true;
                msg.Questions = [question];
                msg.Answers = cacheHits;
                return msg;
            }

            Socket? socket = null;
            try
            {
                socket = new Socket(SocketType.Dgram, ProtocolType.Udp);
                Memory<byte> buffer = new byte[512];
                Message query = new Message();
                query.Questions = [question];

                foreach (IPAddress nsIP in nameservers)
                {
                    int bytes;
                    try
                    {
                        int len = query.ToBytes(buffer.Span);
                        await socket.SendToAsync(buffer.Slice(0, len), SocketFlags.None, new IPEndPoint(nsIP, PORT));
                        bytes = await socket.ReceiveAsync(buffer, SocketFlags.None, new CancellationTokenSource(3000).Token);
                    }
                    catch (SocketException) { continue; }
                    catch (OperationCanceledException) { continue; }

                    try
                    {
                        Message response = new Message(buffer.Slice(0, bytes).Span);

                        //If there is a name error abort and return to the user
                        if (response.ResponseCode == DNSStatus.NameError)
                            return response;

                        //For any other error try a different nameserver
                        if (response.ResponseCode != DNSStatus.OK)
                            continue;

                        //Check if we have a valid answer
                        cache.Store(response.Answers);
                        cache.Store(response.Authorities);
                        cache.Store(response.Additionals);
                        foreach (ResourceRecord answer in response.Answers)
                        {
                            if (answer.Type == question.Type)
                                return response;
                        }
                        foreach (ResourceRecord additional in response.Additionals)
                        {
                            if (question.Name.SequenceEqual(additional.Labels) && additional.Type == question.Type)
                                return response;
                        }

                        //Check if we have a cname redirect
                        foreach (ResourceRecord answer in response.Answers)
                        {
                            if (answer is CNameRecord cname)
                            {
                                question.Name = cname.CNameLabels;
                                return await ResolveQueryInternal(question, nameservers, recursionCount);
                            }
                        }

                        //If not, do we need recursive resolution
                        if (!response.RecursionAvailable && response.Answers.Length == 0 && response.Authorities.Length > 0)
                        {
                            HashSet<string> nextNS = new HashSet<string>();
                            foreach (ResourceRecord authority in response.Authorities)
                            {
                                if (authority is NSRecord ns)
                                    nextNS.Add(ns.NSDomain);
                            }
                            if (nextNS.Count > 0)
                            {
                                HashSet<IPAddress> nextNSIPs = new HashSet<IPAddress>();
                                foreach (ResourceRecord additional in response.Additionals)
                                {
                                    if (nsIP.AddressFamily == AddressFamily.InterNetwork && additional is ARecord a && nextNS.Contains(a.Name))
                                        nextNSIPs.Add(a.Address);
                                    if (nsIP.AddressFamily == AddressFamily.InterNetworkV6 && additional is AAAARecord aaaa && nextNS.Contains(aaaa.Name))
                                        nextNSIPs.Add(aaaa.Address);
                                }

                                //We have a NS without IP
                                if (!nextNSIPs.Any())
                                {
                                    List<IPAddress> addresses;
                                    string nextNameserver = nextNS.First();
                                    cacheHits = cache.Search(new QuestionRecord(nextNameserver, (nsIP.AddressFamily == AddressFamily.InterNetwork) ? DNSRecordType.A : DNSRecordType.AAAA, false));

                                    if (cacheHits?.Length > 0)
                                    {
                                        addresses = new List<IPAddress>();
                                        foreach (ResourceRecord r in cacheHits)
                                        {
                                            if (r is ARecord a)
                                                addresses.Add(a.Address);
                                            else if (r is AAAARecord aaaa)
                                                addresses.Add(aaaa.Address);
                                        }
                                    }
                                    else if (nsIP.AddressFamily == AddressFamily.InterNetwork)
                                        addresses = await ResolveHostV4(nextNameserver);
                                    else
                                        addresses = await ResolveHostV6(nextNameserver);
                                    foreach (IPAddress addr in addresses)
                                        nextNSIPs.Add(addr);
                                }

                                if (nextNSIPs.Any())
                                    return await ResolveQueryInternal(question, nextNSIPs, recursionCount);
                            }
                        }
                    }
                    catch (InvalidDataException ex) { continue; } //Try the next NS
                }
            }
            finally
            {
                socket?.Dispose();
            }
            return null;
        }
    }
}
