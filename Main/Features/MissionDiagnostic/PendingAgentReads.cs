using System;
using System.Collections.Generic;

namespace TAOM.Features.MissionDiagnostic;

// Agents seen at creation and waiting for the next mission tick to be read. Holds object references, never
// Agent.Index (indices recycle). Engine-free so the bookkeeping can be tested without the game.
public sealed class PendingAgentReads<T> where T : class
{
    private readonly List<T> _order = new();
    private readonly HashSet<T> _members = new();

    public void Add(T item)
    {
        if (item != null && _members.Add(item)) _order.Add(item);
    }

    public void Remove(T item)
    {
        if (item != null && _members.Remove(item)) _order.Remove(item);
    }

    public void Clear()
    {
        _order.Clear();
        _members.Clear();
    }

    // Hands every pending item to read, in the order added, and empties the queue first: an item is read at most
    // once, and a reader that throws leaves nothing to retry.
    public void Drain(Action<T> read)
    {
        if (_order.Count == 0) return;
        var batch = _order.ToArray();
        Clear();
        foreach (var item in batch) read(item);
    }
}
