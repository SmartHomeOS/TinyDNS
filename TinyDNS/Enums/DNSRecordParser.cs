namespace TinyDNS.Enums
{
    public static class DNSRecordParser
    {
        public static DNSRecordType Parse(string recordTypeString)
        {
            switch (recordTypeString)
            {
                case "A":
                    return DNSRecordType.A;
                case "AAAA":
                    return DNSRecordType.AAAA;
                case "NS":
                    return DNSRecordType.NS;
                case "SRV":
                    return DNSRecordType.SRV;
                case "PTR":
                    return DNSRecordType.PTR;
                case "TXT":
                    return DNSRecordType.TXT;
                case "CNAME":
                    return DNSRecordType.CNAME;
                default:
                    return DNSRecordType.None;
            }
        }
    }
}
