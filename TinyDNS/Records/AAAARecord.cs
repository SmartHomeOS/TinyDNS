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
    public class AAAARecord : ResourceRecord
    {
        public IPAddress Address { get; protected set; }

        internal AAAARecord(ResourceRecordHeader header, Span<byte> buffer, ref int pos) : base(header, buffer, ref pos)
        {
            ushort len = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(pos, 2));
            pos += 2;
            Address = new IPAddress(buffer.Slice(pos, len));
            pos += len;
        }

        public AAAARecord(IPAddress address, List<string> labels, DNSClass @class, uint ttl) : base(labels, DNSRecordType.AAAA, @class, ttl)
        {
            Address = address;
        }

        public override bool Equals(ResourceRecord? other)
        {
            if (other is AAAARecord otherAAAA)
                return base.Equals(other) && Address.Equals(otherAAAA.Address);
            return false;
        }
    }
}
