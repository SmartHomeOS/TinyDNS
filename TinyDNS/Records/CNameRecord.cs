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

using System.Buffers.Binary;
using TinyDNS.Enums;

namespace TinyDNS.Records
{
    public class CNameRecord : ResourceRecord
    {
        public List<string> CNameLabels { get; }
        public string CName { get { return string.Join('.', CNameLabels); } }

        internal CNameRecord(ResourceRecordHeader header, Span<byte> buffer, ref int pos) : base(header)
        {
            pos += 2;
            CNameLabels = DomainParser.Read(buffer, ref pos);
        }

        public CNameRecord(string cname, List<string> labels, DNSClass @class, uint ttl) : base(labels, DNSRecordType.CNAME, @class, ttl)
        {
            CNameLabels = DomainParser.Parse(cname);
        }

        public CNameRecord(ResourceRecordHeader header, string rdata) : base(header)
        {
            CNameLabels = DomainParser.Parse(rdata);
        }

        public override void Write(Span<byte> buffer, ref int pos)
        {
            base.Write(buffer, ref pos);
            pos += 2;
            int start = pos;
            DomainParser.Write(CNameLabels, buffer, ref pos);
            BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(start - 2, 2), (ushort)(pos- start));
        }

        public override bool Equals(ResourceRecord? other)
        {
            if (other is CNameRecord otherCName)
                return base.Equals(other) && CNameLabels.SequenceEqual(otherCName.CNameLabels);
            return false;
        }

        public override int GetHashCode()
        {
            HashCode hc = GetBaseHash();
            foreach (string label in CNameLabels)
                hc.Add(label);
            return hc.ToHashCode();
        }

        public override string ToString()
        {
            return base.ToString() + $"\t{CName}";
        }
    }
}
