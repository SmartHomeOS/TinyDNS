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

using System.Net;

namespace TinyDNS.Cache
{
    internal struct RecordEntry : IEquatable<RecordEntry>
    {
        public DateTime Time;
        public Message message;
        public IPAddress Address;

        public bool Equals(RecordEntry other)
        {
            return message.Equals(other.message);
        }

        public override bool Equals(object? obj)
        {
            if (obj is RecordEntry entry)
                return Equals(entry);
            return false;
        }

        public override int GetHashCode()
        {
            return message.GetHashCode();
        }
    }
}
