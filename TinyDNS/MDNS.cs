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
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using TinyDNS.Cache;
using TinyDNS.Enums;
using TinyDNS.Events;
using TinyDNS.Records;

namespace TinyDNS
{
    public class MDNS : IDisposable
    {
        public const int PORT = 5353;
        private static readonly IPAddress MulticastAddress = new IPAddress(new byte[] { 224, 0, 0, 251 });
        private static readonly IPAddress MulticastAddressV6 = new IPAddress(new byte[] { 0xFF, 0x02, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0xFB });

        private CancellationTokenSource stop;
        private Socket? listenerV4;
        private Socket? listenerV6;
        private readonly List<Socket> senders = [];

        public delegate Task MessageEventHandler(DNSMessageEvent e);
        public event MessageEventHandler? AnswerReceived;
        public delegate Task ErrorEventHandler(DNSErrorEvent e);
        public event ErrorEventHandler? OnError;
        public delegate Task QueryEventHandler(DNSQueryEvent e);
        public event QueryEventHandler? OnQuery;
        public delegate Task CacheEventHandler(DNSCacheEvent e);
        public event CacheEventHandler? OnRecordExpiration;
        public event CacheEventHandler? OnRecordRefreshTime;
        private readonly ActiveResolverCache cache;
        private readonly bool UNICAST_SUPPORTED;

        public MDNS()
        {
            UNICAST_SUPPORTED = !RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            stop = new CancellationTokenSource();
            cache = new ActiveResolverCache();
            cache.RecordExpiring += Cache_RecordExpiring;
            cache.RecordsExpired += Cache_RecordsExpired;
        }

        private void Cache_RecordsExpired(object? sender, string domain)
        {
            if (OnRecordExpiration != null)
                OnRecordExpiration(new DNSCacheEvent([], domain.Split('.')));
        }

        private async Task Cache_RecordExpiring(object? sender, DNSCacheEvent recordSet)
        {
            if (recordSet.RecordTypes.Contains(DNSRecordType.SRV) || recordSet.RecordTypes.Contains(DNSRecordType.TXT))
            {
                string? service = GetServiceName(recordSet.Domain);
                string? domain = GetDomain(recordSet.Domain);
                if (service != null && domain != null && !string.Join('.', recordSet.Domain).StartsWith(service))
                {
                    string instanceName = GetInstanceName(recordSet.Domain);
                    Console.WriteLine("Updating: " + service);
                    await QueryServiceInstance(instanceName, service, domain, DNSRecordType.SRV, DNSRecordType.A, DNSRecordType.AAAA, DNSRecordType.TXT);
                }
            }
            if (OnRecordRefreshTime != null)
                await OnRecordRefreshTime(recordSet);
        }

        public async Task Start()
        {
            if (Socket.OSSupportsIPv4)
            {
                listenerV4 = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                listenerV4.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    listenerV4.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, false);
            }
            if (Socket.OSSupportsIPv6)
            {
                listenerV6 = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
                listenerV6.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    listenerV6.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.MulticastLoopback, false);

            }
            listenerV4?.Bind(new IPEndPoint(IPAddress.Any, PORT));
            listenerV6?.Bind(new IPEndPoint(IPAddress.IPv6Any, PORT));

            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface nic in nics)
            {
                if (nic.OperationalStatus != OperationalStatus.Up || !nic.SupportsMulticast ||
                    nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel ||
                    nic.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    nic.IsReceiveOnly)
                    continue;
                foreach (UnicastIPAddressInformation address in nic.GetIPProperties().UnicastAddresses)
                {
                    if (address.Address.AddressFamily == AddressFamily.InterNetwork || address.Address.IsIPv6LinkLocal)
                    {
                        Socket socket = new Socket(address.Address.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
                        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        {
                            if (address.Address.AddressFamily == AddressFamily.InterNetwork)
                                socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, false);
                            else
                                socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.MulticastLoopback, false);
                        }
                        socket.Bind(new IPEndPoint(address.Address, PORT));
                        senders.Add(socket);

                        if (listenerV4 != null && address.Address.AddressFamily == AddressFamily.InterNetwork)
                            listenerV4.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(MulticastAddress, address.Address));
                        if (listenerV6 != null && address.Address.AddressFamily == AddressFamily.InterNetworkV6)
                            listenerV6.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.AddMembership, new IPv6MulticastOption(MulticastAddressV6, address.Address.ScopeId));
                    }
                }
            }

            if (listenerV4 != null)
                await Task.Factory.StartNew(ReceiveV4);
            if (listenerV6 != null)
                await Task.Factory.StartNew(ReceiveV6);
        }

        private async Task ReceiveV4()
        {
            IPEndPoint? sender = null;
            try
            {
                Memory<byte> buffer = new byte[8972];
                while (!stop.IsCancellationRequested)
                {
                    try
                    {
                        SocketReceiveFromResult received = await listenerV4!.ReceiveFromAsync(buffer, SocketFlags.None, new IPEndPoint(IPAddress.Any, PORT), stop.Token);
                        sender = (IPEndPoint)received.RemoteEndPoint;
                        if (sender.Port != PORT)
                            continue;
                        Message msg = new Message(buffer.Slice(0, received.ReceivedBytes).Span);
                        if (msg.Response && msg.ResponseCode == DNSStatus.NoError && (msg.Answers.Length > 0 || msg.Additionals.Length > 0))
                        {
                            List<ResourceRecord> updated = new List<ResourceRecord>();
                            List<ResourceRecord> added = new List<ResourceRecord>();
                            foreach (var answer in msg.Answers)
                            {
                                CacheUpdateResult update = cache.Store(answer);
                                if (update == CacheUpdateResult.Update)
                                    updated.Add(answer);
                                else if (update == CacheUpdateResult.NewData)
                                    added.Add(answer);
                            }
                            cache.Store(msg.Additionals);
                            cache.Store(msg.Authorities);
                            if (updated.Count == 0 && added.Count == 0)
                                continue;
                            if (AnswerReceived != null)
                                await AnswerReceived(new DNSMessageEvent(msg, (IPEndPoint)received.RemoteEndPoint, updated, added));
                        }
                        else if (OnQuery != null && !msg.Response && msg.Questions.Length > 0)
                        {
                            await OnQuery.Invoke(new DNSQueryEvent(sender.Address, msg));
                        }
                    }
                    catch (InvalidDataException) { }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (OnError != null)
                    await OnError(new DNSErrorEvent(ex, sender));
            }
        }

        private async Task ReceiveV6()
        {
            IPEndPoint? sender = null;
            try
            {
                Memory<byte> buffer = new byte[8952];
                while (!stop.IsCancellationRequested)
                {
                    try
                    {
                        SocketReceiveFromResult received = await listenerV6!.ReceiveFromAsync(buffer, SocketFlags.None, new IPEndPoint(IPAddress.IPv6Any, PORT), stop.Token);
                        sender = (IPEndPoint)received.RemoteEndPoint;
                        Message msg = new Message(buffer.Slice(0, received.ReceivedBytes).Span);
                        if (msg.Response && msg.ResponseCode == DNSStatus.NoError && (msg.Answers.Length > 0 || msg.Additionals.Length > 0))
                        {
                            List<ResourceRecord> updated = new List<ResourceRecord>();
                            List<ResourceRecord> added = new List<ResourceRecord>();
                            foreach (var answer in msg.Answers)
                            {
                                CacheUpdateResult update = cache.Store(answer);
                                if (update == CacheUpdateResult.Update)
                                    updated.Add(answer);
                                else if (update == CacheUpdateResult.NewData)
                                    added.Add(answer);
                            }
                            cache.Store(msg.Additionals);
                            cache.Store(msg.Authorities);
                            if (updated.Count == 0 && added.Count == 0)
                                continue;
                            if (AnswerReceived != null)
                                await AnswerReceived(new DNSMessageEvent(msg, (IPEndPoint)received.RemoteEndPoint, updated, added));
                        }
                        else if (OnQuery != null && !msg.Response && msg.Questions.Length > 0)
                        {
                            await OnQuery.Invoke(new DNSQueryEvent(sender.Address, msg));
                        }
                    }
                    catch (InvalidDataException) { }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (OnError != null)
                    await OnError(new DNSErrorEvent(ex, sender));
            }
        }

        public async Task<ResourceRecord[]> QueryServices(List<string> serviceFQDN, bool unicastResponse = false)
        {
            ArgumentNullException.ThrowIfNull(serviceFQDN);
            Message msg = new Message();
            msg.Questions = new QuestionRecord[serviceFQDN.Count];
            msg.Response = false;
            HashSet<ResourceRecord> knownAnswers = new HashSet<ResourceRecord>();
            for (int i = 0; i < serviceFQDN.Count; i++)
            {
                msg.Questions[i] = new QuestionRecord(serviceFQDN[i], DNSRecordType.PTR, unicastResponse);
                cache.GetKnownAnswers(serviceFQDN[i], DNSRecordType.PTR).ForEach(r => knownAnswers.Add(r));
            }
            msg.Answers = knownAnswers.ToArray();
            await SendMessage(msg);
            return msg.Answers;
        }

        public async Task<ResourceRecord[]> QueryService(string serviceName, string domain, bool unicastResponse = false)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(serviceName);
            ArgumentNullException.ThrowIfNullOrWhiteSpace(domain);
            string name = string.Concat(serviceName, ".", domain);
            Message msg = new Message();
            msg.Response = false;
            msg.Questions = [
                new QuestionRecord(name, DNSRecordType.PTR, unicastResponse)
            ];
            msg.Answers = cache.GetKnownAnswers(name, DNSRecordType.PTR).ToArray();
            await SendMessage(msg);
            return msg.Answers;
        }

        public async Task<List<Message>> QueryServiceInstance(string instance, string serviceName, string domain, params DNSRecordType[] types)
        {
            string fqdn = string.Concat(instance, ".", serviceName, ".", domain);
            List<Message> responses = new List<Message>();
            List<ResourceRecord> knownRecords = cache.GetKnownAnswers(fqdn, types);


            QuestionRecord[] questions = new QuestionRecord[types.Length];
            for (int i = 0; i < types.Length; i++)
                questions[i] = new QuestionRecord(fqdn, types[i], false);

            await SendQuery(questions, knownRecords.ToArray());
            
            if (knownRecords.Count > 0)
                responses.Add(new Message() { Answers = knownRecords.ToArray() });
            return responses;
        }

        public async Task<List<Message>> ResolveServiceInstance(string instance, string serviceName, string domain, bool unicastResponse = false)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(instance);
            ArgumentNullException.ThrowIfNullOrWhiteSpace(serviceName);
            ArgumentNullException.ThrowIfNullOrWhiteSpace(domain);
            string name = string.Concat(instance, ".", serviceName, ".", domain);
            return await ResolveQuery(name, unicastResponse, DNSRecordType.SRV, DNSRecordType.TXT, DNSRecordType.A, DNSRecordType.AAAA);
        }

        public async Task SendQuery(string domain, DNSRecordType type, bool unicastResponse = false, ResourceRecord[]? knownAnswers = null)
        {
            await SendQuery([new QuestionRecord(domain, type, unicastResponse && UNICAST_SUPPORTED)], knownAnswers);
        }

        public async Task SendQuery(string[] domain, DNSRecordType type, bool unicastResponse = false, ResourceRecord[]? knownAnswers = null)
        {
            await SendQuery([new QuestionRecord(domain, type, unicastResponse && UNICAST_SUPPORTED)], knownAnswers);
        }

        public async Task SendQuery(QuestionRecord[] questions, ResourceRecord[]? knownAnswers = null)
        {
            Message msg = new Message();
            msg.Response = false;
            msg.Questions = questions;
            if (knownAnswers != null)
                msg.Answers = knownAnswers;
            await SendMessage(msg);
        }

        /// <summary>
        /// Lookup the IP addresses for a domain name
        /// </summary>
        /// <param name="fullyQualifiedHost"></param>
        /// <returns>A list of matching IP addresses</returns>
        public async Task<List<IPAddress>> ResolveHost(string fullyQualifiedHost)
        {
            List<IPAddress> iPAddresses = new List<IPAddress>();
            List<Message> responses = await ResolveQuery(fullyQualifiedHost, false, DNSRecordType.A, DNSRecordType.AAAA);
            foreach (Message response in responses)
            {
                foreach (ResourceRecord answer in response.Answers)
                {
                    if (answer is ARecord a)
                        iPAddresses.Add(a.Address);
                    else if (answer is AAAARecord aaaa)
                        iPAddresses.Add(aaaa.Address);
                }
            }
            return iPAddresses;
        }

        /// <summary>
        /// Lookup the domain name for an IP address
        /// </summary>
        /// <param name="address"></param>
        /// <returns>The domain</returns>
        public async Task<string?> ResolveIP(IPAddress address)
        {
            List<Message> responses = await ResolveInverseQuery(address);
            foreach (Message response in responses)
            {
                foreach (ResourceRecord answer in response.Answers)
                {
                    if (answer is PtrRecord ptr)
                    {
                        var labels = ptr.DomainLabels.ToList();
                        if (labels.Count > 1)
                            labels.RemoveAt(labels.Count - 1); //Remove .local
                        return string.Join('.', labels);
                    }
                }
            }
            return null;
        }

        public async Task<List<Message>> ResolveInverseQuery(IPAddress address, bool unicastResponse = false)
        {
            var domain = DomainParser.FromIP(address);
            List<Message> responses = new List<Message>();
            MessageEventHandler handler = delegate (DNSMessageEvent e)
            {
                bool validDomain = false;
                bool validType = false;
                foreach (ResourceRecord answer in e.Message.Answers)
                {
                    if (answer.Labels.SequenceEqual(domain, new DomainEqualityComparer())) //TODO - Rename
                        validDomain = true;
                    if (answer.Type == DNSRecordType.PTR)
                        validType = true;
                }
                if (validDomain && validType)
                    responses.Add(e.Message);
                return Task.CompletedTask;
            };

            AnswerReceived += handler;
            List<ResourceRecord> knownRecords = cache.GetKnownAnswers(string.Join('.', domain), DNSRecordType.PTR);
            if (knownRecords.Count == 0)
            {
                await SendQuery(domain, DNSRecordType.PTR, unicastResponse);
                await Task.Delay(3000);
            }
            else
            {
                responses = [new Message() { Answers = knownRecords.ToArray() }];
            }
            AnswerReceived -= handler;
            return responses;
        }

        public async Task<List<Message>> ResolveQuery(string domain, bool unicastResponse, params DNSRecordType[] types)
        {
            List<Message> responses = new List<Message>();
            List<ResourceRecord> knownRecords = cache.GetKnownAnswers(domain, types);
            MessageEventHandler handler = delegate(DNSMessageEvent e)
            {
                bool validDomain = false;
                bool validType = false;
                foreach (ResourceRecord answer in e.Message.Answers)
                {
                    if (answer.Name.Equals(domain, StringComparison.OrdinalIgnoreCase))
                        validDomain = true;
                    if (types.Contains(answer.Type))
                        validType = true;
                    lock (knownRecords)
                        knownRecords.Remove(answer);
                }
                if (validDomain && validType)
                    responses.Add(e.Message);
                return Task.CompletedTask;
            };
            
            QuestionRecord[] questions = new QuestionRecord[types.Length];
            for (int i = 0; i < types.Length; i++)
                questions[i] = new QuestionRecord(domain, types[i], unicastResponse);
            AnswerReceived += handler;
            await SendQuery(questions, knownRecords.ToArray());
            await Task.Delay(3000);
            AnswerReceived -= handler;
            if (knownRecords.Count > 0)
                responses.Add(new Message() { Answers = knownRecords.ToArray() });
            return responses;
        }

        private async Task SendMessage(Message msg)
        {
            ArraySegment<byte> buffer = ArrayPool<byte>.Shared.Rent(4096);
            msg.TransactionID = 0;
            msg.RecursionDesired = false;
            msg.RecursionAvailable = false;
            try
            {
                int len = msg.ToBytes(buffer, "local");
                foreach (Socket sender in senders)
                {
                    if (sender.AddressFamily == AddressFamily.InterNetwork)
                        await sender.SendToAsync(buffer.Slice(0, len), SocketFlags.None, new IPEndPoint(MulticastAddress, PORT), stop.Token);
                    else
                        await sender.SendToAsync(buffer.Slice(0, len), SocketFlags.None, new IPEndPoint(MulticastAddressV6, PORT), stop.Token);
                    await Task.Delay(5);
                }
            }finally
            {
                ArrayPool<byte>.Shared.Return(buffer.Array!);
            }
        }

        public void Stop()
        {
            stop.Cancel();
            stop = new CancellationTokenSource();
            listenerV4?.Dispose();
            listenerV4 = null;
            listenerV6?.Dispose();
            listenerV6 = null;
            foreach (Socket sender in senders)
                sender.Dispose();
            senders.Clear();
        }

        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
            
        }

        public static string? GetDomain(string[] name)
        {
            int domain;
            for (domain = name.Length - 1; domain > 0; domain--)
            {
                if (name[domain] == "_tcp" || name[domain] == "_udp")
                    break;
            }
            if (domain == 0)
                return null;
            return string.Join('.', name.ToArray(), domain + 1, name.Length - domain - 1);
        }

        //_hap _tcp local
        public static string? GetServiceName(string[] name)
        {
            int domain;
            for(domain = name.Length - 1; domain > 0; domain--)
            {
                if (name[domain] == "_tcp" || name[domain] == "_udp")
                    break;
            }
            if (domain == 0)
                return null;
            if (domain == 1)
                return string.Join('.', name.ToArray(), 0 , domain + 1);
            else
                return string.Join('.', name.ToArray(), 1, domain);
        }

        public static string GetInstanceName(string[] name)
        {
            return name[0];
        }
    }
}
