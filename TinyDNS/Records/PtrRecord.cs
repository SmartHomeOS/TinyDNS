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
    public class PtrRecord : ResourceRecord
    {
        public List<string> DomainLabels { get; }
        public string Domain { get { return string.Join('.', DomainLabels); } }

        internal PtrRecord(ResourceRecordHeader header, Span<byte> buffer, ref int pos) : base(header)
        {
            pos += 2;
            DomainLabels = DomainParser.Read(buffer, ref pos);
        }

        public PtrRecord(string domain, List<string> labels, DNSClass @class, uint ttl) : base(labels, DNSRecordType.PTR, @class, ttl)
        {
            DomainLabels = new List<string>(domain.Split('.'));
        }

        public PtrRecord(ResourceRecordHeader header, string rdata) : base(header)
        {
            DomainLabels = DomainParser.Parse(rdata);
        }

        public override bool Equals(ResourceRecord? other)
        {
            if (other is PtrRecord otherPtr)
                return base.Equals(other) && DomainLabels.SequenceEqual(otherPtr.DomainLabels);
            return false;
        }
    }
}
