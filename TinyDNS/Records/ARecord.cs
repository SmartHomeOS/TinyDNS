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
    public class ARecord : ResourceRecord
    {
        public IPAddress Address { get; protected set; }

        internal ARecord(ResourceRecordHeader header, Span<byte> buffer, ref int pos) : base(header)
        {
            ushort len = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(pos, 2));
            pos += 2;
            Address = new IPAddress(buffer.Slice(pos, len));
            pos += len;
        }

        public ARecord(IPAddress address, List<string> labels, DNSClass @class, uint ttl) : base(labels, DNSRecordType.A, @class, ttl)
        {
            Address = address;
        }

        public ARecord(ResourceRecordHeader header, string rdata) : base(header)
        {
            Address = IPAddress.Parse(rdata);
        }

        public override bool Equals(ResourceRecord? other)
        {
            if (other is ARecord otherA)
                return base.Equals(other) && Address.Equals(otherA.Address);
            return false;
        }

        public override int GetHashCode()
        {
            return Address.GetHashCode() + (int)Type;
        }

        public override string ToString()
        {
            return base.ToString() + $"\t{Address}";
        }
    }
}
