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
    public class ResourceRecordHeader
    {
        public DNSClass Class { get; set; }
        public DNSRecordType Type { get; set; }
        public List<string> Labels { get; set; }
        public DateTime Expires { get; set; }
        public bool CacheFlush { get; set; }

        public ResourceRecordHeader(Span<byte> buffer, ref int pos)
        {
            Labels = DomainParser.Read(buffer, ref pos);
            Type = (DNSRecordType)BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(pos, 2));
            pos += 2;
            ushort recordClass = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(pos, 2));
            pos += 2;
            CacheFlush = (recordClass & 0x8000) == 0x8000;
            Class = (DNSClass)(recordClass & 0x7FFF);
            Expires = DateTime.Now + TimeSpan.FromSeconds(BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(pos, 4)));
            pos += 4;
        }

        public ResourceRecordHeader(string[] columns)
        {
            Labels = DomainParser.Parse(columns[0]);
            Type = DNSRecordParser.Parse(columns[2]);
            uint ttl = uint.Parse(columns[1]);
            Class = DNSClass.Internet;
            Expires = DateTime.Now + TimeSpan.FromSeconds(ttl);
        }

        public ResourceRecordHeader(List<string> labels, DNSRecordType type, DNSClass @class, uint ttl)
        {
            this.Labels = labels;
            this.Type = type;
            this.Class = @class;
            this.Expires = DateTime.Now + TimeSpan.FromSeconds(ttl);
        }

        public void Write(Span<byte> buffer, ref int pos)
        {
            DomainParser.Write(Labels, buffer, ref pos);
            BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(pos, 2), (ushort)Type);
            pos += 2;
            ushort ClassVal = (ushort)Class;
            if (CacheFlush)
                ClassVal |= 0x8000;
            BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(pos, 2), ClassVal);
        }
    }
}
