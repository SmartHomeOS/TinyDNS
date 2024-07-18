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
    public class SRVRecord : ResourceRecord
    {
        public List<string> TargetLabels { get; }
        public ushort Port { get; }
        public ushort Priority { get; }
        public ushort Weight { get; }
        public string Target { get { return string.Join('.', TargetLabels); } }

        internal SRVRecord(ResourceRecordHeader header, Span<byte> buffer, ref int pos) : base(header)
        {
            pos += 2;
            Priority = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(pos, 2));
            pos += 2;
            Weight = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(pos, 2));
            pos += 2;
            Port = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(pos, 2));
            pos += 2;
            TargetLabels = DomainParser.Read(buffer, ref pos);
        }

        public SRVRecord(string service, ushort priority, ushort port, ushort weight, List<string> labels, DNSClass @class, uint ttl) : base(labels, DNSRecordType.SRV, @class, ttl)
        {
            TargetLabels = new List<string>(service.Split('.'));
            Priority = priority;
            Port = port;
            Weight = weight;
        }

        public override bool Equals(ResourceRecord? other)
        {
            if (other is SRVRecord otherSrv)
                return base.Equals(other) && Port == otherSrv.Port && TargetLabels.Equals(otherSrv.TargetLabels);
            return false;
        }
    }
}
