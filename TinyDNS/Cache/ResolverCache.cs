using System.Collections.Concurrent;
using TinyDNS.Records;

namespace TinyDNS.Cache
{
    internal class ResolverCache
    {
        private ConcurrentDictionary<string, HashSet<ResourceRecord>> cache = new ConcurrentDictionary<string, HashSet<ResourceRecord>>();

        public void Store(IEnumerable<ResourceRecord> records)
        {
            foreach (ResourceRecord record in records)
                Store(record);
        }

        public void Store(ResourceRecord record)
        {
            HashSet<ResourceRecord> recordSet = cache.GetOrAdd(record.Name.ToLowerInvariant(), new HashSet<ResourceRecord>());
            lock (recordSet)
            {
                DateTime now = DateTime.Now;
                recordSet.RemoveWhere(r => r.Expires <= now);
                recordSet.Add(record);
            }
        }

        public ResourceRecord[]? Search(QuestionRecord question)
        {
            if (cache.TryGetValue(string.Join('.', question.Name).ToLowerInvariant(), out HashSet<ResourceRecord>? recordSet))
            {
                lock (recordSet)
                {
                    DateTime now = DateTime.Now;
                    recordSet.RemoveWhere(r => r.Expires <= now);
                    return recordSet.Where(r => r.Type == question.Type).ToArray();
                }
            }
            return [];
        }
    }
}
