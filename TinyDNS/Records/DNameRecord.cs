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
using System.Net;
using TinyDNS.Enums;

namespace TinyDNS.Records
{
    public class DNameRecord : ResourceRecord
    {
        public List<string> DNameLabels { get; }
        public string DName { get { return string.Join('.', DNameLabels); } }

        internal DNameRecord(ResourceRecordHeader header, Span<byte> buffer, ref int pos) : base(header)
        {
            pos += 2;
            DNameLabels = DomainParser.Read(buffer, ref pos);
        }

        public DNameRecord(string dname, List<string> labels, DNSClass @class, uint ttl) : base(labels, DNSRecordType.DNAME, @class, ttl)
        {
            DNameLabels = DomainParser.Parse(dname);
        }

        public DNameRecord(ResourceRecordHeader header, string rdata) : base(header)
        {
            DNameLabels = DomainParser.Parse(rdata);
        }

        public override void Write(Span<byte> buffer, ref int pos)
        {
            base.Write(buffer, ref pos);
            pos += 2;
            int start = pos;
            DomainParser.Write(DNameLabels, buffer, ref pos);
            BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(start - 2, 2), (ushort)(pos- start));
        }

        public override bool Equals(ResourceRecord? other)
        {
            if (other is DNameRecord otherDName)
                return base.Equals(other) && DNameLabels.SequenceEqual(otherDName.DNameLabels);
            return false;
        }

        public override int GetHashCode()
        {
            HashCode hc = GetBaseHash();
            foreach (string label in DNameLabels)
                hc.Add(label);
            return hc.ToHashCode();
        }

        public override string ToString()
        {
            return base.ToString() + $"\t{DName}";
        }
    }
}
