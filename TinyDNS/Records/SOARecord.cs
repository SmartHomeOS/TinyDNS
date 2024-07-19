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
    public class SOARecord : ResourceRecord
    {
        public List<string> MNameLabels { get; }
        public List<string> RNameLabels { get; }
        public Version Serial { get; set; }
        public TimeSpan Refresh { get; set; }
        public TimeSpan Retry { get; set; }
        public TimeSpan Expire { get; set; }
        public TimeSpan Minimum { get; set; }
        public string MName { get { return string.Join('.', MNameLabels); } }
        public string RName { get { return string.Join('.', RNameLabels); } }

        internal SOARecord(ResourceRecordHeader header, Span<byte> buffer, ref int pos) : base(header)
        {
            pos += 2;
            MNameLabels = DomainParser.Read(buffer, ref pos);
            RNameLabels = DomainParser.Read(buffer, ref pos);
            Serial = new Version((int)BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(pos, 4)), 0);
            pos += 4;
            Refresh = TimeSpan.FromSeconds(BinaryPrimitives.ReadInt32BigEndian(buffer.Slice(pos, 4)));
            pos += 4;
            Retry = TimeSpan.FromSeconds(BinaryPrimitives.ReadInt32BigEndian(buffer.Slice(pos, 4)));
            pos += 4;
            Expire = TimeSpan.FromSeconds(BinaryPrimitives.ReadInt32BigEndian(buffer.Slice(pos, 4)));
            pos += 4;
            Minimum = TimeSpan.FromSeconds(BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(pos, 4)));
            pos += 4;
        }

        public SOARecord(string mname, string rname, TimeSpan minimum, List<string> labels, DNSClass @class, uint ttl) : base(labels, DNSRecordType.SOA, @class, ttl)
        {
            MNameLabels = DomainParser.Parse(mname);
            RNameLabels = DomainParser.Parse(rname);
            Minimum = minimum;
            Serial = new Version();
        }

        public override bool Equals(ResourceRecord? other)
        {
            if (other is SOARecord otherSOA)
                return base.Equals(other) && MNameLabels.SequenceEqual(otherSOA.MNameLabels) && Serial.Equals(otherSOA.Serial);
            return false;
        }

        public override string ToString()
        {
            return base.ToString() + $"\t{MName}\t{RName}\t{Serial}\t{Refresh}\t{Retry}\t{Expire}\t{Minimum}";
        }
    }
}
