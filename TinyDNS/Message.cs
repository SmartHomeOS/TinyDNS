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

using TinyDNS.Enums;
using TinyDNS.Records;
using System.Buffers.Binary;
using System.Text;

namespace TinyDNS
{
    public sealed class Message : IEquatable<Message>
    {
        public ushort TransactionID { get; set; }
        public bool Response { get; set; }
        public bool RecursionDesired { get; set; }
        public bool RecursionAvailable { get; set; }
        public bool AuthenticData { get; set; }
        public bool CheckingDisabled { get; set; }
        public DNSOperation Operation { get; set; }
        public DNSStatus ResponseCode { get; set; }
        public bool Authoritative { get; set; }

        public Message()
        {
            RecursionDesired = true;
            TransactionID = (ushort)new Random().Next(ushort.MaxValue);
        }
        /// <summary>
        /// Create a DNS Message from a byte buffer
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="length"></param>
        /// <exception cref="InvalidDataException"></exception>
        public Message(Span<byte> buffer)
        {
            TransactionID = BinaryPrimitives.ReadUInt16BigEndian(buffer);
            byte op = buffer[2];
            if ((op & 0x2) == 0x2)
                throw new InvalidDataException("Message Truncated");
            Response = (op & 0x80) == 0x80;
            Authoritative = (op & 0x4) == 0x4; 
            RecursionDesired = (op & 0x1) == 0x1;
            Operation = (DNSOperation)((op & 0x78) >> 3);
            byte response = buffer[3];
            ResponseCode = (DNSStatus)(response & 0xF);
            RecursionAvailable = (response & 0x80) == 0x80;
            AuthenticData = (response & 0x80) == 0x20;
            CheckingDisabled = (response & 0x80) == 0x10;
            ushort questions = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(4, 2));
            Questions = new QuestionRecord[questions];
            ushort answers = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(6, 2));
            Answers = new ResourceRecord[answers];
            ushort authorities = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(8, 2));
            Authorities = new ResourceRecord[authorities];
            ushort additionals = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(10, 2));
            Additionals = new ResourceRecord[additionals];
            int pos = 12;
            for (int i = 0; i < questions; i++)
                Questions[i] = new QuestionRecord(buffer, ref pos);
            for (int i = 0; i < answers; i++)
                Answers[i] = ResourceRecord.Parse(buffer, ref pos);
            for (int i = 0; i < authorities; i++)
                Authorities[i] = ResourceRecord.Parse(buffer, ref pos);
            for (int i = 0; i < additionals; i++)
                Additionals[i] = ResourceRecord.Parse(buffer, ref pos);
        }

        public QuestionRecord[] Questions { get; set; } = [];
        public ResourceRecord[] Answers { get; set; } = [];
        public ResourceRecord[] Authorities { get; set; } = [];
        public ResourceRecord[] Additionals { get; set; } = [];
        public int ToBytes(Span<byte> buffer, string suffix)
        {
            BinaryPrimitives.WriteUInt16BigEndian(buffer, TransactionID);
            byte op = (byte)(((byte)Operation & 0xF) << 3);
            if (Response)
                op |= 0x80;
            if (Authoritative)
                op |= 0x4;
            //Not Truncated
            if (RecursionDesired)
                op |= 0x1;
            buffer[2] = op;
            byte rcode = (byte)((byte)ResponseCode & 0xF);
            if (RecursionAvailable)
                rcode |= 0x80;
            if (AuthenticData)
                rcode |= 0x20;
            if (CheckingDisabled)
                rcode |= 0x10;
            buffer[3] = rcode;
            BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(4, 2), (ushort)Questions.Length);
            BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(6, 2), (ushort)Answers.Length);
            BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(8, 2), (ushort)Authorities.Length);
            BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(10, 2), (ushort)Additionals.Length);
            int pos = 12;
            foreach (QuestionRecord question in Questions)
                question.Write(buffer, ref pos, suffix);
            foreach (ResourceRecord answer in Answers)
                answer.Write(buffer, ref pos);
            foreach (ResourceRecord authority in Authorities)
                authority.Write(buffer, ref pos);
            foreach (ResourceRecord additional in Additionals)
                additional.Write(buffer, ref pos);
            return pos;
        }

        public bool Equals(Message? other)
        {
            if (other == null)
                return false;
            if (Questions.Length == other.Questions.Length && Answers.Length == other.Answers.Length && Additionals.Length == other.Additionals.Length)
            {
                for (int i = 0; i < Answers.Length; i++)
                {
                    if (!Array.Exists(other.Answers, a => Answers[i].Equals(a)))
                        return false;
                }
                for (int i = 0; i < Additionals.Length; i++)
                {
                    if (!Array.Exists(other.Additionals, a => Additionals[i].Equals(a)))
                        return false;
                }
                for (int i = 0; i < Questions.Length; i++)
                {
                    if (!Array.Exists(other.Questions, a => Questions[i].Equals(a)))
                        return false;
                }
                return true;
            }
            return false;
        }

        public override bool Equals(object? obj)
        {
            if (obj is Message message)
                return Equals(message);
            return false;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(";; Operation: ");
            sb.Append(Operation);
            sb.Append(", Status: ");
            sb.Append(ResponseCode);
            sb.Append(", ID: ");
            sb.AppendLine(TransactionID.ToString());
            sb.Append(";; Flags:");
            if (Response)
                sb.Append(" qr");
            if (RecursionDesired)
                sb.Append(" rd");
            if (RecursionAvailable)
                sb.Append(" ra");
            sb.Append("; QUERY: ");
            sb.Append(Questions.Length.ToString());
            sb.Append(", ANSWER: ");
            sb.Append(Answers.Length.ToString());
            sb.Append(", AUTHORITY: ");
            sb.Append(Authorities.Length.ToString());
            sb.Append(", ADDITIONAL: ");
            sb.AppendLine(Additionals.Length.ToString());
            sb.AppendLine();
            if (Questions.Length > 0)
            {
                sb.AppendLine(";; Questions:");
                foreach (QuestionRecord question in Questions)
                    sb.AppendLine(question.ToString());
                sb.AppendLine();
            }
            if (Answers.Length > 0)
            {
                sb.AppendLine(";; Answers:");
                foreach (ResourceRecord answer in Answers)
                    sb.AppendLine(answer.ToString());
                sb.AppendLine();
            }
            if (Authorities.Length > 0)
            {
                sb.AppendLine(";; Authority:");
                foreach (ResourceRecord authority in Authorities)
                    sb.AppendLine(authority.ToString());
                sb.AppendLine();
            }
            return sb.ToString();
        }

        public override int GetHashCode()
        {
            return base.GetHashCode() + TransactionID;
        }
    }
}
