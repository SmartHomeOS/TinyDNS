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

using System.Collections.Concurrent;
using System.Net;

namespace TinyDNS
{
    internal sealed class RecordCache
    {
        private struct RecordEntry : IEquatable<RecordEntry>
        {
            public Message message;
            public IPAddress Address;

            public bool Equals(RecordEntry other)
            {
                if (Address.Equals(other.Address))
                    return message.Equals(other.message);

                return false;
            }
        }

        readonly int sizeLimit;
        private readonly ConcurrentStack<RecordEntry> stack = new ConcurrentStack<RecordEntry>();

        public RecordCache(int sizeLimit)
        {
            this.sizeLimit = sizeLimit;
        }

        public void Clear()
        {
            stack.Clear();
        }

        public bool Cached(Message message, IPAddress endPoint)
        {
            RecordEntry entry = new RecordEntry();
            entry.message = message;
            entry.Address = endPoint;
            if (stack.Contains(entry))
                return true;
            stack.Push(entry);
            if (stack.Count > sizeLimit)
                stack.TryPop(out _);
            return false;
        }
    }
}
