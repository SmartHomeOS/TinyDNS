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
using System.Text;
using TinyDNS.Enums;

namespace TinyDNS.Records
{
    public class TxtRecord : ResourceRecord
    {
        public List<string> Strings { get; }

        internal TxtRecord(ResourceRecordHeader header, Span<byte> buffer, ref int pos) : base(header, buffer, ref pos)
        {
            List<string> strings = [];
            ushort rLen = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(pos, 2));
            pos += 2;
            while (rLen > 0)
            {
                byte len = buffer[pos++];
                rLen--;
                if (len > 0)
                {
                    strings.Add(Encoding.UTF8.GetString(buffer.Slice(pos, len)));
                    pos += len;
                    rLen -= len;
                }
            }
            if (rLen < 0)
                throw new InvalidDataException("Currupted TXT record");
            Strings = strings;
        }

        public TxtRecord(List<string> strings, List<string> labels, DNSClass @class, uint ttl) : base(labels, DNSRecordType.TXT, @class, ttl)
        {
            Strings = strings;
        }

        public override bool Equals(ResourceRecord? other)
        {
            if (other is TxtRecord otherTxt)
                return base.Equals(other) && Strings.Equals(otherTxt.Strings);
            return false;
        }
    }
}