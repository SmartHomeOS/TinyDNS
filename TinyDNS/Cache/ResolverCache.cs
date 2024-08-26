using System.Collections.Concurrent;
using TinyDNS.Enums;
using TinyDNS.Records;

namespace TinyDNS.Cache
{
    internal class ResolverCache
    {
        protected static readonly TimeSpan RECENT = TimeSpan.FromSeconds(2);
        protected ConcurrentDictionary<string, HashSet<ResourceRecord>> cache = new ConcurrentDictionary<string, HashSet<ResourceRecord>>();

        /// <summary>
        /// Store the resource records in the cache
        /// </summary>
        /// <param name="records"></param>
        /// <returns>True if any record is new. False if all records existed</returns>
        public void Store(IEnumerable<ResourceRecord> records)
        {
            foreach (ResourceRecord record in records)
                Store(record);
        }

        /// <summary>
        /// Store the resource record in the cache
        /// </summary>
        /// <param name="record"></param>
        /// <returns>The effect of the update on the cache</returns>
        public virtual CacheUpdateResult Store(ResourceRecord record)
        {
            if (record.Type == DNSRecordType.OPT || record is UnsupportedRecord)
                return CacheUpdateResult.NoUpdate;
            HashSet<ResourceRecord> recordSet = cache.GetOrAdd(record.Name.ToLowerInvariant(), new HashSet<ResourceRecord>());
            DateTime now = DateTime.Now;
            lock (recordSet)
            {
                Prune(recordSet, now);
                if (record.CacheFlush)
                    recordSet.RemoveWhere(r => r.Type == record.Type && (now - record.Created) >= RECENT );
                bool updated = recordSet.Remove(record);
                recordSet.Add(record);
                return updated ? CacheUpdateResult.Update : CacheUpdateResult.NewData;
            }
        }

        public virtual ResourceRecord[] Search(QuestionRecord question)
        {
            if (cache.TryGetValue(question.Name.ToLowerInvariant(), out HashSet<ResourceRecord>? recordSet))
            {
                DateTime now = DateTime.Now;
                lock (recordSet)
                {
                    Prune(recordSet, now);
                    return recordSet.Where(r => r.Type == question.Type).ToArray();
                }
            }
            return [];
        }

        protected int Prune(HashSet<ResourceRecord> recordSet, DateTime now)
        {
            return recordSet.RemoveWhere(r => r.Expires <= now);
        }
    }
}
