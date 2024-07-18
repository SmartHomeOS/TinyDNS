using System.Net;
using System.Text;
using TinyDNS.Properties;
using TinyDNS.Records;

namespace TinyDNS
{
    public static class DNSSources
    {
        public static List<IPAddress> RootNameservers
        {
            get
            {
                List<IPAddress> addresses = new List<IPAddress>();
                string rootHints = Encoding.UTF8.GetString(Resources.named);
                List<ResourceRecord> recordSet = GetResourceRecords(rootHints);
                foreach (ResourceRecord record in recordSet)
                {
                    if (record is ARecord a)
                        addresses.Add(a.Address);
                    else if (record is AAAARecord aaaa)
                        addresses.Add(aaaa.Address);
                }
                return addresses;
            }
        }

        public static List<IPAddress> CloudflareDNSAddresses
        {
            get
            {
                return
                [
                    new IPAddress(new byte[]{1, 1, 1, 1}),
                new IPAddress(new byte[]{1, 0, 0, 1})
                ];
            }
        }

        public static List<IPAddress> GoogleDNSAddresses
        {
            get
            {
                return
                [
                    new IPAddress(new byte[]{8, 8, 8, 8}),
                new IPAddress(new byte[]{8, 8, 4, 4})
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
