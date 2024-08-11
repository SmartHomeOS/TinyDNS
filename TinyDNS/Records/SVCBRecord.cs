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

using System;
using System.Buffers.Binary;
using System.Net;
using System.Text;
using TinyDNS.Enums;

namespace TinyDNS.Records
{
    public class SvcbRecord : ResourceRecord
    {
        public int ServicePriority { get; }
        public List<string> TargetName { get; }
        public Dictionary<string, List<string>> Parameters { get; }

        internal SvcbRecord(ResourceRecordHeader header, Span<byte> buffer, ref int pos) : base(header)
        {
            Dictionary<string, List<string>> param = new();
            ushort rLen = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(pos, 2));
            pos += 2;
            int initialPos = pos;
            ServicePriority = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(pos, 2));
            pos += 2;
            TargetName = DomainParser.Read(buffer, ref pos);
            rLen -= (ushort)(pos - initialPos);
            while (rLen > 0)
            {
                ushort key = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(pos, 2));
                pos += 2;
                ushort len = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(pos, 2));
                pos += 2;
                rLen -= 4;
                if (len > 0)
                {
                    param.Add(GetKey(key), GetValue(key, buffer.Slice(pos, len)));
                    pos += len;
                    rLen -= len;
                }
                else
                    param.Add(GetKey(key), []);
            }
            if (rLen < 0)
                throw new InvalidDataException("Currupted SVCB record");
            Parameters = param;
        }

        public SvcbRecord(Dictionary<string, List<string>> @params, List<string> targetName, ushort priority, List<string> labels, DNSClass @class, uint ttl) : base(labels, DNSRecordType.SVCB, @class, ttl)
        {
            Parameters = @params;
            TargetName = targetName;
            ServicePriority = priority;
        }

        public override bool Equals(ResourceRecord? other)
        {
            if (other is SvcbRecord otherSvcb)
                return base.Equals(other) && Parameters.Equals(otherSvcb.Parameters);
            return false;
        }

        private string GetKey(ushort keyIndex)
        {
            switch (keyIndex)
            {
                case 0:
                    return "mandatory";
                case 1:
                    return "alpn";
                case 2:
                    return "no-default-alpn";
                case 3:
                    return "port";
                case 4:
                    return "ipv4hint";
                case 5:
                    return "ech";
                case 6:
                    return "ipv6hint";
                case 7:
                    return "dohpath";
                case 8:
                    return "ohttp";
                default:
                    return "unknown";
            }
        }

        private List<string> GetValue(ushort key, Span<byte> span)
        {
            List<string> strings = [];
            switch (key)
            {
                case 1:
                case 2:
                    int pos = 0;
                    while (pos < span.Length)
                    {
                        byte len = span[pos++];
                        if (len > 0)
                        {
                            strings.Add(Encoding.UTF8.GetString(span.Slice(pos, len)));
                            pos += len;
                        }
                    }
                    break;
                case 3:
                    strings.Add(BinaryPrimitives.ReadUInt16BigEndian(span).ToString());
                    break;
                case 4:
                    for (int i = 0; i < span.Length; i += 4)
                        strings.Add(new IPAddress(span.Slice(i, 4)).ToString());
                    break;
                case 6:
                    for (int i = 0; i < span.Length; i += 16)
                        strings.Add(new IPAddress(span.Slice(i, 16)).ToString());
                    break;
                default:
                    strings.Add(Encoding.UTF8.GetString(span));
                    break;
            }
            return strings;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var kvp in Parameters)
            {
                if (sb.Length > 0)
                    sb.Append(' ');
                sb.Append(kvp.Key);
                sb.Append("=");
                sb.Append(string.Join(',', kvp.Value));
            }
            return base.ToString() + $"\t{sb}";
        }
    }
}
