using System.Collections.Generic;
using TAOM.Core.Collections;

namespace TAOM.Features.Stamina.Domain;

/// <summary>
/// Which soldiers get their stats recomputed this frame. A recompute (Agent.UpdateAgentProperties) runs the whole stat
/// model, and a formation that fights together changes band together, so the calls are spread: at most a budget per
/// frame, the player's soldier first, and never a soldier whose spawn may still be in progress. A soldier waits in the
/// queue once, however often his band changes meanwhile. Main thread only.
/// </summary>
public sealed class StaminaRefreshQueue<TKey>
    where TKey : class
{
    private readonly List<Entry> _entries = new List<Entry>();
    private readonly HashSet<TKey> _queued = new HashSet<TKey>(ReferenceIdentity.Instance);

    public int Count => _entries.Count;

    public void Enqueue(TKey key, float bornAt, bool first)
    {
        if (_queued.Add(key))
            _entries.Add(new Entry(key, bornAt, first));
    }

    /// <summary>
    /// Fills <paramref name="output"/> with at most <paramref name="budget"/> soldiers old enough to refresh, the
    /// ones queued as first before the others, each group in queue order. A soldier too young stays queued.
    /// </summary>
    public void Take(float now, int budget, float minAgeSeconds, List<TKey> output)
    {
        output.Clear();
        TakePass(now, budget, minAgeSeconds, output, first: true);
        TakePass(now, budget, minAgeSeconds, output, first: false);
    }

    public void Clear()
    {
        _entries.Clear();
        _queued.Clear();
    }

    private void TakePass(float now, int budget, float minAgeSeconds, List<TKey> output, bool first)
    {
        var index = 0;
        while (index < _entries.Count && output.Count < budget)
        {
            var entry = _entries[index];
            // A positive requirement: an age that is not a number never passes.
            if (entry.First != first || !(now - entry.BornAt >= minAgeSeconds))
            {
                index++;
                continue;
            }

            output.Add(entry.Key);
            _queued.Remove(entry.Key);
            _entries.RemoveAt(index);
        }
    }

    private readonly struct Entry
    {
        internal Entry(TKey key, float bornAt, bool first)
        {
            Key = key;
            BornAt = bornAt;
            First = first;
        }

        internal TKey Key { get; }

        internal float BornAt { get; }

        internal bool First { get; }
    }
}
