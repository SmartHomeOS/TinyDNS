﻿// TinyDNS Copyright (C) 2024 
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
    public class ARecord : IPRecord
    {
        internal ARecord(ResourceRecordHeader header, Span<byte> buffer, ref int pos) : base(header, buffer, ref pos) { }

        public ARecord(IPAddress address, string[] labels, DNSClass @class, uint ttl) : base(address, labels, DNSRecordType.A, @class, ttl) { }

        public ARecord(ResourceRecordHeader header, string rdata) : base(header, rdata) { }

        public override void Write(Span<byte> buffer, ref int pos)
        {
            base.Write(buffer, ref pos);
            BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(pos, 2), 4);
            pos += 2;
            Address.TryWriteBytes(buffer.Slice(pos, 4), out _);
            pos += 4;
        }

        public override bool Equals(ResourceRecord? other)
        {
            if (other is ARecord otherA)
                return base.Equals(other) && Address.Equals(otherA.Address);
            return false;
        }

        public override int GetHashCode()
        {
            HashCode hc = GetBaseHash();
            hc.Add(Address);
            return hc.ToHashCode();
        }

        public override string ToString()
        {
            return base.ToString() + $"\t{Address}";
        }
    }
}
