using SenseNet.Client;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Linq;
using System.Threading.Tasks;

namespace SyncAD2Portal
{
    public static class Extensions
    {
        // =================================================================================================== Collections

        public static Task ForEachAsync<T>(this IEnumerable<T> source, int degreeOfParalellism, Func<T, Task> action)
        {
            return Task.WhenAll(Partitioner.Create(source).GetPartitions(degreeOfParalellism).Select(partition => Task.Run(async () =>
            {
                using (partition)
                    while (partition.MoveNext())
                        await action(partition.Current);
            })));
        }
        public static Task ForEachAsync(this SearchResultCollection source, int degreeOfParalellism, Func<SearchResult, Task> action)
        {
            return Task.WhenAll(Partitioner.Create(source.Cast<SearchResult>()).GetPartitions(degreeOfParalellism).Select(partition => Task.Run(async () =>
            {
                using (partition)
                    while (partition.MoveNext())
                        await action(partition.Current);
            })));
        }
        public static bool ContainsPath(this IEnumerable<Content> contentCollection, string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            return contentCollection.Any(c => string.CompareOrdinal(c.Path, path) == 0);
        }
        public static bool ContainsValue(this PropertyValueCollection propValueColl, string value)
        {
            return propValueColl.Cast<object>().Contains(value);
        }

        // =================================================================================================== Content

        public static Content GetContent(this IDictionary<string, Content> dictionary, string guid)
        {
            Content c;
            return dictionary.TryGetValue(guid, out c) ? c : null;
        }

        // =================================================================================================== String

        public static string MaximizeLength(this string s, int max)
        {
            if ((s == null) || (max <= 0))
                return s;
            return s.Length > max ? s.Substring(0, max) : s;
        }
        public static string PrefixDeleted(this string s)
        {
            return string.Concat(DateTime.UtcNow.ToString("yyMMddHHmm"), "-", s);
        }

        // =================================================================================================== SearchResult

        public static string GetParentPath(this SearchResult sr)
        {
            return sr.Path.Substring(sr.Path.IndexOf(",", StringComparison.Ordinal) + 1);
        }
        public static string GetParentPath(this ADSearchResult sr)
        {
            return sr.Path.Substring(sr.Path.IndexOf(",", StringComparison.Ordinal) + 1);
        }
        public static ADSearchResult ToADSearchResult(this SearchResult result, Server server)
        {
            var propColl = result.Properties[Common.ADPropertyNames.WhenChanged];
            var whenChanged = propColl != null && propColl.Count > 0 ? Convert.ToDateTime(propColl[0]) : DateTime.MinValue;

            return new ADSearchResult(server)
            {
                Path = result.Path,
                SyncGuid = Common.GetADResultGuid(result, server.GuidProperty),
                WhenChanged = whenChanged
            };
        }
        public static ADSearchResult ToADSearchResult(this DirectoryEntry entry, Server server)
        {
            var propColl = entry.Properties[Common.ADPropertyNames.WhenChanged];
            var whenChanged = propColl != null && propColl.Count > 0 ? Convert.ToDateTime(propColl[0]) : DateTime.MinValue;

            return new ADSearchResult(server)
            {
                Path = entry.Path,
                SyncGuid = entry.Guid,
                WhenChanged = whenChanged
            };
        }
    }
}
