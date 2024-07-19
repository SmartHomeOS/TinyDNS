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

        private readonly CancellationTokenSource stop = new CancellationTokenSource();
        private readonly Socket? listenerV4;
        private readonly Socket? listenerV6;
        private readonly List<Socket> senders = [];

        public delegate Task MessageEventHandler(DNSMsgEvent e);
        public event MessageEventHandler? AnswerReceived;
        private readonly RecordCache messageCache = new RecordCache(100, TimeSpan.FromSeconds(5));
        private readonly bool UNICAST_SUPPORTED;

        public MDNS()
        {
            UNICAST_SUPPORTED = !RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

            if (Socket.OSSupportsIPv4)
            {
                listenerV4 = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                listenerV4.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            }
            if (Socket.OSSupportsIPv6)
            {
                listenerV6 = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
                listenerV6.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            }
        }

        public async Task Start()
        {
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
            try
            {
                Memory<byte> buffer = new byte[8972];
                while (!stop.IsCancellationRequested)
                {
                    try
                    {
                        SocketReceiveFromResult received = await listenerV4!.ReceiveFromAsync(buffer, SocketFlags.None, new IPEndPoint(IPAddress.Any, PORT), stop.Token);
                        Message msg = new Message(buffer.Slice(0, received.ReceivedBytes).Span);
                        if (msg.Response && msg.ResponseCode == DNSStatus.NoError && (msg.Answers.Length > 0 || msg.Additionals.Length > 0))
                        {
                            if (messageCache.Cached(msg, ((IPEndPoint)received.RemoteEndPoint).Address))
                                continue;
                            if (AnswerReceived != null)
                                await AnswerReceived(new DNSMsgEvent(msg, (IPEndPoint)received.RemoteEndPoint));
                        }
                    }
                    catch (InvalidDataException) { }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                //TODO: logger.Error(ex, "Failed to parse DNS answer");
            }
        }

        private async Task ReceiveV6()
        {
            try
            {
                Memory<byte> buffer = new byte[8952];
                while (!stop.IsCancellationRequested)
                {
                    try
                    {
                        SocketReceiveFromResult received = await listenerV6!.ReceiveFromAsync(buffer, SocketFlags.None, new IPEndPoint(IPAddress.IPv6Any, PORT), stop.Token);
                        Message msg = new Message(buffer.Slice(0, received.ReceivedBytes).Span);
                        if (msg.Response && msg.ResponseCode == DNSStatus.NoError && (msg.Answers.Length > 0 || msg.Additionals.Length > 0))
                        {
                            if (messageCache.Cached(msg, ((IPEndPoint)received.RemoteEndPoint).Address))
                                continue;
                            if (AnswerReceived != null)
                                await AnswerReceived(new DNSMsgEvent(msg, (IPEndPoint)received.RemoteEndPoint));
                        }
                    }
                    catch (InvalidDataException) { }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                //TODO: logger.Error(ex, "Failed to parse DNS answer");
            }
        }

        public async Task QueryAny(string domain, bool unicastResponse = false)
        {
            messageCache.Clear();
            Message msg = new Message();
            msg.Response = false;
            msg.Questions = [
                new QuestionRecord(domain, DNSRecordType.ANY, unicastResponse && UNICAST_SUPPORTED)
            ];
            await SendMessage(msg);
        }

        public async Task QueryServices(string domain)
        {
            Message msg = new Message();
            msg.Response = false;
            msg.Questions = [
                new QuestionRecord(domain, DNSRecordType.SRV, false),
                new QuestionRecord(domain, DNSRecordType.TXT, false)
            ];
            await SendMessage(msg);
        }

        public async Task QueryTxts(string domain)
        {
            Message msg = new Message();
            msg.Response = false;
            msg.Questions = [
                new QuestionRecord(domain, DNSRecordType.TXT, false)
            ];
            await SendMessage(msg);
        }

        public async Task QueryPointers(string domain)
        {
            Message msg = new Message();
            msg.Response = false;
            msg.Questions = [
                new QuestionRecord(domain, DNSRecordType.PTR, false)
            ];
            await SendMessage(msg);
        }

        public async Task QueryIPv4Addresses(string domain)
        {
            Message msg = new Message();
            msg.Response = false;
            msg.Questions = [
                new QuestionRecord(domain, DNSRecordType.A, false)
            ];
            await SendMessage(msg);
        }

        public async Task QueryIPv6Addresses(string domain)
        {
            Message msg = new Message();
            msg.Response = false;
            msg.Questions = [
                new QuestionRecord(domain, DNSRecordType.AAAA, false)
            ];
            await SendMessage(msg);
        }

        private async Task SendMessage(Message msg)
        {
            Memory<byte> buffer = new byte[512];
            msg.TransactionID = 0;
            msg.RecursionDesired = false;
            msg.RecursionAvailable = false;
            int len = msg.ToBytes(buffer.Span);
            foreach (Socket sender in senders)
            {
                if (sender.AddressFamily == AddressFamily.InterNetwork)
                    await sender.SendToAsync(buffer.Slice(0, len), SocketFlags.None, new IPEndPoint(MulticastAddress, PORT), stop.Token);
                else
                    await sender.SendToAsync(buffer.Slice(0, len), SocketFlags.None, new IPEndPoint(MulticastAddressV6, PORT), stop.Token);
            }
        }

        public void Stop()
        {
            stop.Cancel();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            listenerV4?.Dispose();
            listenerV6?.Dispose();
            foreach (Socket sender in senders)
                sender.Dispose();
        }
    }
}
