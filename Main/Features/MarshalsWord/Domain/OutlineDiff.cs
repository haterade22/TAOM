using System.Collections.Generic;

namespace TAOM.Features.MarshalsWord.Domain;

/// <summary>The pure plan of build-spec 8.2: who to paint and who to clear, against what was last painted. Only a key in <c>painted</c> is ever cleared.</summary>
internal static class OutlineDiff
{
    internal static void Plan<TKey>(IReadOnlyDictionary<TKey, uint> painted, IReadOnlyDictionary<TKey, uint> wanted,
        List<KeyValuePair<TKey, uint>> toPaint, List<TKey> toClear)
        where TKey : notnull
    {
        toPaint.Clear();
        toClear.Clear();
        foreach (var want in wanted)
        {
            if (!painted.TryGetValue(want.Key, out var colour) || colour != want.Value)
                toPaint.Add(want);
        }

        foreach (var key in painted.Keys)
        {
            if (!wanted.ContainsKey(key))
                toClear.Add(key);
        }
    }
}
