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

using TinyDNS.Enums;

namespace TinyDNS.Records
{
    public class ResourceRecord : IEquatable<ResourceRecord>
    {
        public DNSClass Class { get { return header.Class; } set { header.Class = value; } }
        public DNSRecordType Type { get { return header.Type; } set { header.Type = value; } }
        public List<string> Labels { get { return header.Labels; } set { header.Labels = value; } }
        public DateTime Expires { get { return header.Expires; } set { header.Expires = value; } }
        public bool CacheFlush { get { return header.CacheFlush; } set { header.CacheFlush = value; } }
        public string Name { get { return string.Join('.', header.Labels); } }
        protected ResourceRecordHeader header;

        public ResourceRecord(ResourceRecordHeader header)
        {
            this.header = header;
        }

        public ResourceRecord(List<string> labels, DNSRecordType type, DNSClass @class, uint ttl)
        {
            this.header = new ResourceRecordHeader(labels, type, @class, ttl);
        }

        public static ResourceRecord Parse(Span<byte> buffer, ref int pos)
        {
            ResourceRecordHeader header = new ResourceRecordHeader(buffer, ref pos);
            ResourceRecord record;
            switch (header.Type)
            {
                case DNSRecordType.A:
                    record = new ARecord(header, buffer, ref pos);
                    break;
                case DNSRecordType.PTR:
                    record = new PtrRecord(header, buffer, ref pos);
                    break;
                case DNSRecordType.AAAA:
                    record = new AAAARecord(header, buffer, ref pos);
                    break;
                case DNSRecordType.SRV:
                    record = new SRVRecord(header, buffer, ref pos);
                    break;
                case DNSRecordType.TXT:
                    record = new TxtRecord(header, buffer, ref pos);
                    break;
                case DNSRecordType.CNAME:
                    record = new CNameRecord(header, buffer, ref pos);
                    break;
                case DNSRecordType.NS:
                    record = new NSRecord(header, buffer, ref pos);
                    break;
                case DNSRecordType.SOA:
                    record = new SOARecord(header, buffer, ref pos);
                    break;
                default:
                    record = new UnsupportedRecord(header, buffer, ref pos);
                    break;
            }
            return record;
        }

        public void Write(Span<byte> buffer, ref int pos)
        {
            header.Write(buffer, ref pos);
        }

        public virtual bool Equals(ResourceRecord? other)
        {
            return Type == other!.Type && Name.SequenceEqual(other!.Name);
        }

        internal static ResourceRecord Parse(string line)
        {
            string[] columns = line.Split(' ', 4, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            ResourceRecordHeader header = new ResourceRecordHeader(columns);
            ResourceRecord record;
            switch (header.Type)
            {
                case DNSRecordType.A:
                    record = new ARecord(header, columns[3]);
                    break;
                case DNSRecordType.PTR:
                    record = new PtrRecord(header, columns[3]);
                    break;
                case DNSRecordType.AAAA:
                    record = new AAAARecord(header, columns[3]);
                    break;
                case DNSRecordType.CNAME:
                    record = new CNameRecord(header, columns[3]);
                    break;
                case DNSRecordType.NS:
                    record = new NSRecord(header, columns[3]);
                    break;
                default:
                    record = new UnsupportedRecord(header, columns[3]);
                    break;
            }
            return record;
        }

        public override string ToString()
        {
            int ttl = (int)Math.Ceiling((Expires - DateTime.Now).TotalSeconds);
            return $"{Name}\t{ttl}\t{Class}\t{Type}";
        }
    }
}
