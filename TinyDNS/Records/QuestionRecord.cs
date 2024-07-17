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
using TinyDNS.Enums;

namespace TinyDNS.Records
{
    public class QuestionRecord : IEquatable<QuestionRecord>
    {
        public List<string> Name { get; set; }
        public DNSRecordType Type { get; set; }
        public DNSClass Class { get; set; }
        public bool UnicastResponse { get; set; }
        public QuestionRecord(string domain, DNSRecordType recordType, bool unicastResponse) : this(new List<string>(domain.Split('.')), recordType, unicastResponse) {  }

        public QuestionRecord(List<string> domain, DNSRecordType recordType, bool unicastResponse)
        { 
            Name = domain;
            Type = recordType;
            Class = DNSClass.Internet;
            UnicastResponse = unicastResponse;
        }

        internal QuestionRecord(Span<byte> buffer, ref int pos)
        {
            Name = DomainParser.Read(buffer, ref pos);
            Type = (DNSRecordType)BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(pos, 2));
            pos += 2;
            Class = (DNSClass)BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(pos, 2));
            pos += 2;
            UnicastResponse = ((ushort)Class & 0x8000) == 0x8000;
            Class = (DNSClass)((ushort)Class & 0x7FFF);
        }

        public void Write(Span<byte> buffer, ref int pos)
        {
            DomainParser.Write(Name, buffer, ref pos);
            BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(pos, 2), (ushort)Type);
            pos += 2;
            ushort ClassVal = (ushort)Class;
            if (UnicastResponse)
                ClassVal |= 0x8000;
            BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(pos, 2), ClassVal);
            pos += 2;
        }

        public virtual bool Equals(QuestionRecord? other)
        {
            if (other == null)
                return false;
            return Name.SequenceEqual(other.Name);
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as QuestionRecord);
        }
    }
}