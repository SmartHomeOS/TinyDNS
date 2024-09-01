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
        public string[] MNameLabels { get; }
        public string[] RNameLabels { get; }
        public uint Serial { get; set; }
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
            Serial = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(pos, 4));
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

        public SOARecord(string mname, string rname, TimeSpan minimum, string[] labels, DNSClass @class, uint ttl) : base(labels, DNSRecordType.SOA, @class, ttl)
        {
            MNameLabels = DomainParser.Parse(mname);
            RNameLabels = DomainParser.Parse(rname);
            Minimum = minimum;
        }

        public override void Write(Span<byte> buffer, ref int pos)
        {
            base.Write(buffer, ref pos);
            pos += 2;
            int start = pos;
            DomainParser.Write(MNameLabels, buffer, ref pos);
            DomainParser.Write(RNameLabels, buffer, ref pos);
            BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(pos, 4), Serial);
            pos += 4;
            BinaryPrimitives.WriteInt32BigEndian(buffer.Slice(pos, 4), (int)Refresh.TotalSeconds);
            pos += 4;
            BinaryPrimitives.WriteInt32BigEndian(buffer.Slice(pos, 4), (int)Retry.TotalSeconds);
            pos += 4;
            BinaryPrimitives.WriteInt32BigEndian(buffer.Slice(pos, 4), (int)Expire.TotalSeconds);
            pos += 4;
            BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(pos, 4), (uint)Minimum.TotalSeconds);
            pos += 4;
            BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(start - 2, 2), (ushort)(pos - start));
        }

        public override bool Equals(ResourceRecord? other)
        {
            if (other is SOARecord otherSOA)
                return base.Equals(other) && MNameLabels.SequenceEqual(otherSOA.MNameLabels) && Serial.Equals(otherSOA.Serial);
            return false;
        }

        public override int GetHashCode()
        {
            HashCode hc = GetBaseHash();
            hc.Add(Serial);
            foreach (string label in MNameLabels)
                hc.Add(label);
            foreach (string label in RNameLabels)
                hc.Add(label);
            return hc.ToHashCode();
        }

        public override string ToString()
        {
            return base.ToString() + $"\t{MName}\t{RName}\t{Serial}\t{Refresh}\t{Retry}\t{Expire}\t{Minimum}";
        }
    }
}
