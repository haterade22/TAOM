using System;
using System.Collections.Generic;
using BehaviorTreeWrapper;

namespace TAOM.Features.MissionDiagnostic;

// Agents seen at creation and waiting for the next mission tick to be read. Holds object references, never
// Agent.Index (indices recycle). Engine-free so the bookkeeping can be tested without the game.
// Threads: the mission tick drains this queue on the main mission thread. Native raises OnAgentRemoved and
// OnAgentDeleted on the thread it chooses (#634, #607), so RemoveOrDefer parks an off-thread removal and the next
// Drain applies it before it reads anything.
public sealed class PendingAgentReads<T> where T : class
{
    private readonly List<T> _order = new();
    private readonly HashSet<T> _members = new();
    private readonly DeferredCallbackQueue _deferred;

    // report: where a site's first off-thread removal is reported. It runs on the reporting thread, so it must be the
    // thread-safe file log, never an on-screen logger.
    public PendingAgentReads(Action<string>? report = null) => _deferred = new DeferredCallbackQueue(report);

    public int Count => _members.Count;

    public void Add(T item)
    {
        if (item != null && _members.Add(item)) _order.Add(item);
    }

    public void Remove(T item)
    {
        if (item != null && _members.Remove(item)) _order.Remove(item);
    }

    // Remove on the main mission thread; anywhere else the removal waits for the next Drain. The parked call keeps
    // only the reference: Remove hashes and compares it and never reads the item, so an agent the engine deletes in
    // between is safe (Agent.GetHashCode returns a managed field, and Agent does not override Equals).
    public void RemoveOrDefer(string site, T item)
    {
        if (item != null) _deferred.RunOrDefer(site, () => Remove(item));
    }

    // Drops the pending items and every parked removal (mission end, a failed read).
    public void Clear()
    {
        ClearItems();
        _deferred.Clear();
    }

    // Applies the parked removals, then hands every pending item to read, in the order added, and empties the queue
    // first: an item is read at most once, and a reader that throws leaves nothing to retry.
    public void Drain(Action<T> read)
    {
        _deferred.Drain();
        if (_order.Count == 0) return;
        var batch = _order.ToArray();
        ClearItems();
        foreach (var item in batch) read(item);
    }

    private void ClearItems()
    {
        _order.Clear();
        _members.Clear();
    }
}
