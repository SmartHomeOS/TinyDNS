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
using TinyDNS.Events;
using TinyDNS.Records;

namespace TinyDNS.Cache
{
    internal sealed class ActiveResolverCache : ResolverCache, IDisposable
    {
        public delegate Task AsyncEventHandler(object sender, DNSCacheEvent e);
        public event AsyncEventHandler? RecordExpiring;
        public event EventHandler<string>? RecordsExpired;

        private CancellationTokenSource cts = new CancellationTokenSource();
        public ActiveResolverCache() : base()
        {
            Task.Run(Curate, cts.Token);
        }

        /// <summary>
        /// Store the resource record in the cache
        /// </summary>
        /// <param name="record"></param>
        /// <returns>The effect of the update on the cache</returns>
        public override CacheUpdateResult Store(ResourceRecord record)
        {
            if (record.Type == DNSRecordType.NSEC || record.Type == DNSRecordType.OPT || record is UnsupportedRecord)
                return CacheUpdateResult.NoUpdate;
            CacheUpdateResult updated = CacheUpdateResult.NoUpdate;
            HashSet<ResourceRecord> recordSet = cache.GetOrAdd(record.Name.ToLowerInvariant(), new HashSet<ResourceRecord>());
            DateTime now = DateTime.Now;
            lock (recordSet)
            {
                if (record.CacheFlush)
                    recordSet.RemoveWhere(r => r.Type == record.Type && (now - record.Created) > RECENT);
                if (recordSet.TryGetValue(record, out ResourceRecord? oldRecord))
                {
                    recordSet.Remove(oldRecord);
                    if ((oldRecord.Created + RECENT) < now)
                        updated = CacheUpdateResult.Update;
                }
                else
                    updated = CacheUpdateResult.NewData;
                record.CacheFlush = false;
                recordSet.Add(record);
                return updated;
            }
        }

        public List<ResourceRecord> GetKnownAnswers(string domain, params DNSRecordType[] types)
        {
            if (cache.TryGetValue(domain.ToLowerInvariant(), out HashSet<ResourceRecord>? recordSet))
            {
                DateTime now = DateTime.Now;
                lock (recordSet)
                {
                    Prune(recordSet, now);
                    return recordSet.Where(r => types.Contains(r.Type) && ((r.Expires - now) / (r.Expires - r.Created)) > 0.5).ToList();
                }
            }
            return [];
        }

        private async Task Curate()
        {
            while (!cts.Token.IsCancellationRequested)
            {
                DateTime now = DateTime.Now;
                foreach (var kvp in cache)
                {
                    int pruned;
                    IEnumerable<ResourceRecord> expiring;
                    lock (kvp.Value)
                    {
                        pruned = Prune(kvp.Value, now);
                        expiring = kvp.Value.Where(r => !r.Stale && ((r.Expires - now) / (r.Expires - r.Created)) < 0.125);
                    }
                    if (expiring.Any())
                    {
                        expiring.All(r => r.Stale = true);
                        RecordExpiring?.Invoke(this, new DNSCacheEvent(expiring.Select(r => r.Type).Distinct().ToArray(), kvp.Key.Split('.')));
                    }
                    else if (pruned > 0)
                        RecordsExpired?.Invoke(this, kvp.Key);
                }
                await Task.Delay(4000);
            }
        }

        public void Dispose()
        {
            cts.Cancel();
            cts.Dispose();
        }
    }
}
