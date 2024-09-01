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
    public class PtrRecord : ResourceRecord
    {
        public string[] DomainLabels { get; }
        public string Domain { get { return string.Join('.', DomainLabels); } }

        internal PtrRecord(ResourceRecordHeader header, Span<byte> buffer, ref int pos) : base(header)
        {
            pos += 2;
            DomainLabels = DomainParser.Read(buffer, ref pos);
        }

        public PtrRecord(string domain, string[] labels, DNSClass @class, uint ttl) : base(labels, DNSRecordType.PTR, @class, ttl)
        {
            DomainLabels = DomainParser.Parse(domain);
        }

        public PtrRecord(ResourceRecordHeader header, string rdata) : base(header)
        {
            DomainLabels = DomainParser.Parse(rdata);
        }

        public override void Write(Span<byte> buffer, ref int pos)
        {
            base.Write(buffer, ref pos);
            pos += 2;
            int start = pos;
            DomainParser.Write(DomainLabels, buffer, ref pos);
            BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(start - 2, 2), (ushort)(pos- start));
        }

        public override bool Equals(ResourceRecord? other)
        {
            if (other is PtrRecord otherPtr)
                return base.Equals(other) && DomainLabels.SequenceEqual(otherPtr.DomainLabels);
            return false;
        }

        public override int GetHashCode()
        {
            HashCode hc = GetBaseHash();
            foreach (var label in DomainLabels)
                hc.Add(label);
            return hc.ToHashCode();
        }

        public override string ToString()
        {
            return base.ToString() + $"\t{Domain}";
        }
    }
}
