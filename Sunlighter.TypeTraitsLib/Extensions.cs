using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Sunlighter.TypeTraitsLib
{
    public static partial class Extensions
    {
        public static ImmutableSortedSet<T> UnionAll<T>(this ImmutableSortedSet<T> set, IEnumerable<ImmutableSortedSet<T>> items)
        {
            foreach (ImmutableSortedSet<T> otherSet in items)
            {
                set = set.Union(otherSet);
            }

            return set;
        }

        public static ImmutableSortedDictionary<K, V2> Map<K, V1, V2>(this ImmutableSortedDictionary<K, V1> dict, Func<K, V1, V2> func)
#if !NETSTANDARD2_0
            where K : notnull
#endif
        {
            var result = ImmutableSortedDictionary<K, V2>.Empty.WithComparers(dict.KeyComparer).ToBuilder();
            foreach (var kvp in dict)
            {
                result.Add(kvp.Key, func(kvp.Key, kvp.Value));
            }
            return result.ToImmutable();
        }
    }
}
