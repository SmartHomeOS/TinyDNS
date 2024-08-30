using System.Net;
using System.Text;
using TinyDNS.Properties;
using TinyDNS.Records;

namespace TinyDNS
{
    public static class DNSSources
    {
        public static List<Nameserver> RootNameservers
        {
            get
            {
                List<Nameserver> addresses = new List<Nameserver>();
                string rootHints = Encoding.UTF8.GetString(Resources.named);
                List<ResourceRecord> recordSet = GetResourceRecords(rootHints);
                foreach (ResourceRecord record in recordSet)
                {
                    if (record is ARecord a)
                        addresses.Add(new Nameserver(a.Address, false));
                    else if (record is AAAARecord aaaa)
                        addresses.Add(new Nameserver(aaaa.Address, false));
                }
                return addresses;
            }
        }

        public static List<Nameserver> CloudflareDNS
        {
            get
            {
                return
                [
                    new Nameserver(new IPAddress(new byte[]{1, 1, 1, 1}), true),
                    new Nameserver(new IPAddress(new byte[]{1, 0, 0, 1}), true)
                ];
            }
        }

        public static List<Nameserver> GoogleDNS
        {
            get
            {
                return
                [
                    new Nameserver(new IPAddress(new byte[]{8, 8, 8, 8}), true),
                    new Nameserver(new IPAddress(new byte[]{8, 8, 4, 4}), true)
                ];
            }
        }

        public static List<ResourceRecord> GetResourceRecords(string dnsZone)
        {
            List<ResourceRecord> records = new List<ResourceRecord>();
            foreach (string line in dnsZone.Split('\n', StringSplitOptions.TrimEntries))
            {
                //Skip Comments
                if (line.StartsWith(';'))
                    continue;

                records.Add(ResourceRecord.Parse(line));
            }
            return records;
        }
    }
}
