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
using System.Text;
using TinyDNS.Enums;

namespace TinyDNS.Records
{
    public class UnsupportedRecord : ResourceRecord
    {
        public byte[] RData { get; protected set; }

        internal UnsupportedRecord(ResourceRecordHeader header, Span<byte> buffer, ref int pos) : base(header)
        {
            ushort len = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(pos, 2));
            pos += 2;
            if (pos+len > buffer.Length)
            {
                len = (ushort)(buffer.Length - pos);
            }
            RData = buffer.Slice(pos, len).ToArray();
            pos += len;
        }

        public UnsupportedRecord(byte[] data, List<string> labels, DNSRecordType type, DNSClass @class, uint ttl) : base(labels, type, @class, ttl)
        {
            RData = data;
        }

        public UnsupportedRecord(ResourceRecordHeader header, string rdata) : base(header)
        {
            RData = Encoding.UTF8.GetBytes(rdata);
        }

        public override void Write(Span<byte> buffer, ref int pos)
        {
            base.Write(buffer, ref pos);
            BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(pos, 2), (ushort)RData.Length);
            pos += 2;
            RData.CopyTo(buffer.Slice(pos, RData.Length));
            pos += RData.Length;
        }

        public override bool Equals(ResourceRecord? other)
        {
            if (other is UnsupportedRecord unsupported)
                return base.Equals(other) && RData.Equals(unsupported.RData);
            return false;
        }

        public override int GetHashCode()
        {
            HashCode hc = GetBaseHash();
            hc.Add(RData);
            return hc.ToHashCode();
        }

        public override string ToString()
        {
            return base.ToString() + $"\t{RData.Length} Bytes";
        }
    }
}
