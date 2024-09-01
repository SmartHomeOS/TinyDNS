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

using System.Buffers;
using System.Net;
using System.Net.Http.Headers;
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
        private readonly HashSet<Nameserver> globalNameservers = [];
        private ResolverCache cache = new ResolverCache();
        private ResolutionMode resolutionMode;
        /// <summary>
        /// Create a new DNS resolver
        /// </summary>
        /// <param name="mode">Which strategy to use to resolve queries</param>
        public DNSResolver(ResolutionMode mode = ResolutionMode.InsecureOnly)
        {
            this.resolutionMode = mode;
            ReloadNameservers();
            NetworkChange.NetworkAddressChanged += (s, e) => ReloadNameservers();
        }
        /// <summary>
        /// Create a new DNS resolver
        /// </summary>
        /// <param name="nameservers">the nameservers to query</param>
        /// <param name="mode">Which strategy to use to resolve queries</param>
        public DNSResolver(List<Nameserver> nameservers, ResolutionMode mode = ResolutionMode.InsecureOnly)
        {
            this.resolutionMode = mode;
            foreach (Nameserver nameserver in nameservers)
                this.globalNameservers.Add(nameserver);
        }

        /// <summary>
        /// The nameservers to query
        /// </summary>
        public List<Nameserver> NameServers
        { 
            get
            { 
                return globalNameservers.ToList(); 
            } 
            set
            {
                globalNameservers.Clear();
                foreach (Nameserver nameserver in value)
                    globalNameservers.Add(nameserver);
            }
        }

        private void ReloadNameservers()
        {
            globalNameservers.Clear();
            var nics = NetworkInterface.GetAllNetworkInterfaces();
            IPAddress? gateway = null;
            string dnsSuffix = string.Empty;

            //Gateway DNS servers first
            foreach (var nic in nics)
            {
                if (nic.OperationalStatus == OperationalStatus.Up && !nic.IsReceiveOnly && (nic.GetIPProperties().GatewayAddresses.Count > 0) && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback && nic.NetworkInterfaceType != NetworkInterfaceType.Unknown)
                {
                    foreach (IPAddress ns in nic.GetIPProperties().DnsAddresses)
                        globalNameservers.Add(new Nameserver(ns, nic.GetIPProperties().DnsSuffix));
                    if (gateway == null)
                        dnsSuffix = nic.GetIPProperties().DnsSuffix;
                    gateway ??= nic.GetIPProperties().GatewayAddresses.FirstOrDefault()?.Address;
                }
            }
            //Then fallback to all DNS servers
            foreach (var nic in nics)
            {
                if (nic.OperationalStatus == OperationalStatus.Up && !nic.IsReceiveOnly && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                {
                    foreach (IPAddress ns in nic.GetIPProperties().DnsAddresses)
                        globalNameservers.Add(new Nameserver(ns, nic.GetIPProperties().DnsSuffix));
                    if (gateway == null)
                        dnsSuffix = nic.GetIPProperties().DnsSuffix;
                    gateway ??= nic.GetIPProperties().GatewayAddresses.FirstOrDefault()?.Address;
                }
            }

            if (gateway != null)
                globalNameservers.Add(new Nameserver(gateway, dnsSuffix)); //Always fall back to the gateway
        }

        /// <summary>
        /// Resolve a hostname to it's IP addresses
        /// </summary>
        /// <param name="hostname">domain name</param>
        /// <returns>domain's IPs</returns>
        public async Task<List<IPAddress>> ResolveHost(string hostname)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(hostname, nameof(hostname));
            List<IPAddress> addresses = 
                [
                    .. await ResolveHostV4(hostname),
                    .. await ResolveHostV6(hostname)
                ];
            return addresses;
        }

        /// <summary>
        /// Resolve a hostname to it's IP v4 addresses
        /// </summary>
        /// <param name="hostname">domain name</param>
        /// <returns>domain's IPs</returns>
        public async Task<List<IPAddress>> ResolveHostV4(string hostname)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(hostname, nameof(hostname));
            List<IPAddress> addresses = new List<IPAddress>();
            Message? response = await ResolveQuery(new QuestionRecord(hostname, DNSRecordType.A, false));
            if (response == null || response.ResponseCode != DNSStatus.NoError || (response.Answers.Length == 0 && response.Additionals.Length == 0))
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

        /// <summary>
        /// Resolve a hostname to it's IPv6 addresses
        /// </summary>
        /// <param name="hostname">domain name</param>
        /// <returns>domain's IPs</returns>
        public async Task<List<IPAddress>> ResolveHostV6(string hostname)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(hostname, nameof(hostname));
            List<IPAddress> addresses = new List<IPAddress>();
            Message? response = await ResolveQuery(new QuestionRecord(hostname, DNSRecordType.AAAA, false));
            if (response == null || response.ResponseCode != DNSStatus.NoError || (response.Answers.Length == 0 && response.Additionals.Length == 0))
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

        /// <summary>
        /// Lookup the domain name for an IP address
        /// </summary>
        /// <param name="address"></param>
        /// <returns>The response message</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public async Task<Message?> ResolveIPRecord(IPAddress address)
        {
            if (address == null)
                throw new ArgumentNullException(nameof(address));
            byte[] addressBytes = address.GetAddressBytes();
            bool privateQuery = IsPrivate(address, addressBytes);
            return await ResolveQuery(new QuestionRecord(DomainParser.FromIP(addressBytes), DNSRecordType.PTR, false), privateQuery);
        }

        /// <summary>
        /// Lookup the domain name for an IP address
        /// </summary>
        /// <param name="address"></param>
        /// <returns>The domain</returns>
        public async Task<string?> ResolveIP(IPAddress address)
        {
            Message? response = await ResolveIPRecord(address);
            if (response == null || response.ResponseCode != DNSStatus.NoError)
                return null;

            foreach (ResourceRecord answer in response.Answers)
            {
                if (answer is PtrRecord ptr)
                    return ptr.Domain;
            }
            return null;
        }

        /// <summary>
        /// Sends a query and returns the response message
        /// </summary>
        /// <param name="question"></param>
        /// <returns></returns>
        public async Task<Message?> ResolveQuery(QuestionRecord question)
        {
            bool privateQuery = (question.NameLabels[question.NameLabels.Length - 1] == "local" || question.NameLabels.Length == 1);
            return await ResolveQueryInternal(question, globalNameservers, privateQuery);
        }

        private async Task<Message?> ResolveQuery(QuestionRecord question, bool privateQuery)
        {
            return await ResolveQueryInternal(question, globalNameservers, privateQuery);
        }

        private async Task<Message?> ResolveQueryInternal(QuestionRecord question, HashSet<Nameserver> nameservers, bool privateQuery, int recursionCount = 0)
        {
            //Check for excessive recursion
            recursionCount++;
            if (recursionCount > 10)
                return null;

            //Check for cache hits
            ResourceRecord[] cacheHits = cache.Search(question);
            if (cacheHits != null && cacheHits.Length > 0)
            {
                Message msg = new Message();
                msg.Response = true;
                msg.Questions = [question];
                msg.Answers = cacheHits;
                return msg;
            }

            //Otherwise query the nameserver(s)
            Socket? socket = null;
            try
            {
                socket = new Socket(SocketType.Dgram, ProtocolType.Udp);
                byte[] buffer = ArrayPool<byte>.Shared.Rent(512);
                try
                {
                    Message query = new Message();
                    query.Questions = [question];

                    foreach (Nameserver ns in nameservers)
                    {
                        //Prevent leaking local domains into the global DNS space
                        if (privateQuery && !IsPrivate(ns.Address))
                            continue;

                        int bytes;
                        try
                        {
                            if (resolutionMode == ResolutionMode.InsecureOnly || (resolutionMode == ResolutionMode.SecureWithFallback && ns.SupportsDoH == false))
                                bytes = await ResolveUDP(query, buffer, socket, ns);
                            else
                            {
                                try
                                {
                                    if (ns.SupportsDoH == false)
                                        continue;
                                    bytes = await ResolveHTTPS(query, buffer, ns);
                                }
                                catch (HttpRequestException)
                                {
                                    if (resolutionMode == ResolutionMode.SecureOnly)
                                        continue;
                                    bytes = await ResolveUDP(query, buffer, socket, ns);
                                }
                                catch (OperationCanceledException)
                                {
                                    if (resolutionMode == ResolutionMode.SecureOnly)
                                        continue;
                                    bytes = await ResolveUDP(query, buffer, socket, ns);
                                }
                            }
                        }
                        catch (SocketException) { continue; }
                        catch (OperationCanceledException) { continue; }

                        try
                        {
                            Message response = new Message(buffer.AsSpan().Slice(0, bytes));

                            //For any error try a different nameserver
                            if (response.ResponseCode != DNSStatus.NoError)
                                continue;

                            //Add new info to the cache
                            cache.Store(response.Answers);
                            cache.Store(response.Authorities);
                            cache.Store(response.Additionals);

                            //Check if we have a valid answer
                            foreach (ResourceRecord answer in response.Answers)
                            {
                                if (answer.Type == question.Type)
                                    return response;
                            }
                            foreach (ResourceRecord additional in response.Additionals)
                            {
                                if (question.NameLabels.SequenceEqual(additional.Labels, new DomainEqualityComparer()) && additional.Type == question.Type)
                                    return response;
                            }

                            //Check if we have a cname redirect
                            foreach (ResourceRecord answer in response.Answers)
                            {
                                if (answer is CNameRecord cname)
                                {
                                    question.NameLabels = cname.CNameLabels;
                                    return await ResolveQueryInternal(question, nameservers, privateQuery, recursionCount);
                                }
                            }

                            //If not, do we need recursive resolution
                            if (!response.RecursionAvailable && response.Answers.Length == 0 && response.Authorities.Length > 0)
                            {
                                HashSet<string> nextNS = new HashSet<string>();
                                foreach (ResourceRecord authority in response.Authorities)
                                {
                                    if (authority is NSRecord nsr)
                                        nextNS.Add(nsr.NSDomain);
                                }
                                if (nextNS.Count > 0)
                                {
                                    HashSet<Nameserver> nextNSIPs = new HashSet<Nameserver>();
                                    foreach (ResourceRecord additional in response.Additionals)
                                    {
                                        if (ns.Address.AddressFamily == AddressFamily.InterNetwork && additional is ARecord a && nextNS.Contains(a.Name))
                                            nextNSIPs.Add(new Nameserver(a.Address));
                                        if (ns.Address.AddressFamily == AddressFamily.InterNetworkV6 && additional is AAAARecord aaaa && nextNS.Contains(aaaa.Name))
                                            nextNSIPs.Add(new Nameserver(aaaa.Address));
                                    }

                                    //We have a NS without IP
                                    if (!nextNSIPs.Any())
                                    {
                                        List<IPAddress> addresses;
                                        string nextNameserver = nextNS.First();
                                        cacheHits = cache.Search(new QuestionRecord(nextNameserver, (ns.Address.AddressFamily == AddressFamily.InterNetwork) ? DNSRecordType.A : DNSRecordType.AAAA, false));

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
                                        else if (ns.Address.AddressFamily == AddressFamily.InterNetwork)
                                            addresses = await ResolveHostV4(nextNameserver);
                                        else
                                            addresses = await ResolveHostV6(nextNameserver);
                                        foreach (IPAddress addr in addresses)
                                            nextNSIPs.Add(new Nameserver(addr));
                                    }

                                    if (nextNSIPs.Any())
                                        return await ResolveQueryInternal(question, nextNSIPs, privateQuery, recursionCount);
                                }
                            }
                        }
                        catch (InvalidDataException) { continue; } //Try the next NS
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
            finally
            {
                socket?.Dispose();
            }
            return null;
        }

        private static bool IsPrivate(IPAddress ip, byte[]? addr = null)
        {
            if (ip.IsIPv6UniqueLocal || ip.IsIPv6SiteLocal || ip.IsIPv6LinkLocal || IPAddress.IsLoopback(ip))
                return true;
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                if (addr == null)
                    addr = ip.GetAddressBytes();
                if ((addr[0] == 169 && addr[1] == 254) || (addr[0] == 192 && addr[1] == 168) ||
                    (addr[0] == 10) || (addr[0] == 172 && (addr[1] & 0xF0) == 0x10))
                    return true;
            }
            return false;
        }

        private async Task<int> ResolveUDP(Message query, Memory<byte> buffer, Socket socket, Nameserver nameserver)
        {
            int len = query.ToBytes(buffer.Span, nameserver.DNSSuffix);
            await socket.SendToAsync(buffer.Slice(0, len), SocketFlags.None, new IPEndPoint(nameserver.Address, PORT));
            return await socket.ReceiveAsync(buffer, SocketFlags.None, new CancellationTokenSource(3000).Token);
        }

        private async Task<int> ResolveHTTPS(Message query, Memory<byte> buffer, Nameserver nameserver)
        {
            query.TransactionID = 0;
            int len = query.ToBytes(buffer.Span, nameserver.DNSSuffix);
            using (HttpClient httpClient = new HttpClient())
            {
                ByteArrayContent content = new ByteArrayContent(buffer.Slice(0, len).ToArray());
                content.Headers.ContentType = new MediaTypeHeaderValue("application/dns-message");
                string hostname = nameserver.Address.ToString();
                if (nameserver.Address.AddressFamily == AddressFamily.InterNetworkV6)
                    hostname = String.Concat("[", hostname, "]");
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, $"https://{hostname}/dns-query");
                request.Content = content;
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/dns-message"));
                request.Version = new Version(2, 0);
                request.VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
                var response = await httpClient.SendAsync(request, new CancellationTokenSource(3000).Token);
                response.EnsureSuccessStatusCode();
                var tempBuff = await response.Content.ReadAsByteArrayAsync();
                tempBuff.CopyTo(buffer);
                return tempBuff.Length;
            }
        }
    }
}
