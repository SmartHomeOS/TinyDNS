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

namespace TinyDNS.Cache
{
    internal sealed class RecordCache
    {
        readonly int sizeLimit;
        readonly TimeSpan ttl;
        private readonly ConcurrentQueue<RecordEntry> cache = new ConcurrentQueue<RecordEntry>();

        public RecordCache(int sizeLimit, TimeSpan ttl)
        {
            this.sizeLimit = sizeLimit;
            this.ttl = ttl;
        }

        public void Clear()
        {
            cache.Clear();
        }

        public bool Cached(Message message, IPAddress endPointIP)
        {
            Expire();
            RecordEntry entry = new RecordEntry();
            entry.message = message;
            entry.Address = endPointIP;
            entry.Time = DateTime.Now;
            lock (cache)
            {
                if (cache.Contains(entry))
                    return true;
                cache.Enqueue(entry);
            }
            if (cache.Count > sizeLimit)
                cache.TryDequeue(out _);
            return false;
        }

        private void Expire()
        {
            while (cache.TryPeek(out RecordEntry entry))
            {
                if (entry.Time + ttl > DateTime.Now)
                    return;
                cache.TryDequeue(out _);
            }
        }
    }
}
