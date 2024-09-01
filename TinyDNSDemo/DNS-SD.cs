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

using System.Collections.Concurrent;
using TinyDNS;
using TinyDNS.Enums;
using TinyDNS.Events;
using TinyDNS.Records;

namespace TinyDNSDemo
{
    public class DNSSD
    {
        public const string ALL_SERVICES = "_services._dns-sd._udp";
        public const string DEFAULT_DOMAIN = "local";
        MDNS mdns;
        ConcurrentDictionary<string, ConcurrentDictionary<string, HashSet<ResourceRecord>>> cache = new();

        public DNSSD()
        {
            mdns = new MDNS();
        }

        public async Task Run()
        {
            Console.WriteLine("Scanning - Will update every 10 secs");
            await Task.Factory.StartNew(Print);
            mdns.AnswerReceived += Mdns_AnswerReceived;
            await mdns.Start();

            var records = await mdns.QueryService(ALL_SERVICES, DEFAULT_DOMAIN, true);
            await ProcessRecords(records);

            await Task.Delay(10000);
            records = await mdns.QueryService(ALL_SERVICES, DEFAULT_DOMAIN, true);
            await ProcessRecords(records);
        }

        private async Task Mdns_AnswerReceived(DNSMessageEvent e)
        {
            await ProcessRecords(e.AddedRecords.ToArray());
        }

        private async Task ProcessRecords(ResourceRecord[] records)
        {
            foreach (ResourceRecord record in records)
            {
                if (record.Type == DNSRecordType.PTR)
                {
                    string? serviceName;
                    PtrRecord item = (PtrRecord)record;
                    if (!item.Domain.EndsWith(".local"))
                        return; //NOT DNS-SD
                    if (item.Name.StartsWith(ALL_SERVICES))
                    {
                        serviceName = MDNS.GetServiceName(item.DomainLabels);
                        var cachedAnswers = await mdns.QueryService(serviceName!, DEFAULT_DOMAIN);
                        if (serviceName != null)
                            updateGlobal(serviceName);
                        await ProcessRecords(cachedAnswers);
                    }
                    else if ((item.Domain.EndsWith("_tcp.local") || item.Domain.EndsWith("_udp.local")))
                    {
                        serviceName = MDNS.GetServiceName(item.DomainLabels);
                        if (serviceName == ALL_SERVICES)
                            continue;
                        string? serviceInstance = MDNS.GetInstanceName(item.DomainLabels);
                        
                        if (serviceName != null)
                        {
                            if (UpdateService(serviceName, serviceInstance))
                            {
                                var lst = await mdns.ResolveServiceInstance(serviceInstance!, serviceName!, DEFAULT_DOMAIN);
                                foreach (var msg in lst)
                                    await ProcessRecords(msg.Answers);
                            }
                        }
                    }
                }
                else if (record.Type == DNSRecordType.SRV)
                {
                    string? serviceName = MDNS.GetServiceName(((SRVRecord)record).Labels);
                    string? serviceInstance = MDNS.GetInstanceName(((SRVRecord)record).Labels);
                    List<ResourceRecord> rcds = new List<ResourceRecord>();
                    IEnumerable<ResourceRecord> txtRecords = records.Where(r => r.Type == DNSRecordType.TXT && record.Name.Equals(r.Name));
                    rcds.AddRange(txtRecords);
                    rcds.Add(record);
                    if (serviceInstance != null && serviceName != null)
                        UpdateInstance(serviceName, serviceInstance, rcds);
                }
                else if (record.Type == DNSRecordType.TXT)
                {
                    if (records.Any(r => r.Type == DNSRecordType.SRV && record.Name.Equals(r.Name)))
                        continue;
                    string? serviceName = MDNS.GetServiceName(((TxtRecord)record).Labels);
                    string? serviceInstance = MDNS.GetInstanceName(((TxtRecord)record).Labels);
                    if (serviceInstance != null && serviceName != null)
                        UpdateInstance(serviceName, serviceInstance, [record]);
                }
            }
        }

        private bool UpdateInstance(string service, string instance, List<ResourceRecord> records)
        {
            var sr = cache.GetOrAdd(service, new ConcurrentDictionary<string, HashSet<ResourceRecord>>());
            var ir = sr.GetOrAdd(instance, new HashSet<ResourceRecord>());
            bool success = false;
            lock (ir)
            {
                foreach (var record in records)
                    success |= ir.Add(record);
            }
            return success;
        }

        private bool UpdateService(string service, string instance)
        {
            var sr = cache.GetOrAdd(service, new ConcurrentDictionary<string, HashSet<ResourceRecord>>());
            return sr.TryAdd(instance, new HashSet<ResourceRecord>());
        }

        private bool updateGlobal(string service)
        {
            return cache.TryAdd(service, new ConcurrentDictionary<string, HashSet<ResourceRecord>>());
        }

        private async Task Print()
        {
            while (true)
            {
                await Task.Delay(10000);

                Console.Clear();
                Console.WriteLine("**********************************************************************************************");
                foreach (var service in cache)
                {
                    Console.WriteLine(service.Key + ": ");
                    foreach (var serviceInstance in service.Value)
                    {
                        Console.WriteLine("\t* " + serviceInstance.Key);
                        foreach (ResourceRecord record in serviceInstance.Value)
                            Console.WriteLine("\t\t- " + record.Type + "\t" + record.Name);
                    }
                }
            }
        }
    }
}
