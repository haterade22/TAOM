using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using TAOM.Core.Collections;
using TAOM.Features.Stamina.Domain;

namespace TAOM.Features.Stamina;

/// <summary>
/// The stamina cells of one battle, keyed by the soldier's object (never an agent index: the engine hands a deleted
/// agent's index to the next one). Two views of the same cells: a concurrent dictionary that any thread reads
/// (<see cref="TryGet"/>), and a roster list that only the mission tick walks. A new soldier is queued from the engine
/// callback and admitted by the tick, so the roster has one writer. Generic so the tests need no engine object.
/// </summary>
public sealed class StaminaStore<TKey>
    where TKey : class
{
    private readonly ConcurrentDictionary<TKey, StaminaCell> _cells = new ConcurrentDictionary<TKey, StaminaCell>(ReferenceIdentity.Instance);
    private readonly ConcurrentQueue<TKey> _pending = new ConcurrentQueue<TKey>();
    private readonly List<KeyValuePair<TKey, StaminaCell>> _roster = new List<KeyValuePair<TKey, StaminaCell>>();

    /// <summary>Main thread: how many soldiers the roster holds, the removed ones the tick has not dropped yet included.</summary>
    public int Count => _roster.Count;

    /// <summary>Any thread: a soldier the next tick admits.</summary>
    public void Enqueue(TKey key) => _pending.Enqueue(key);

    /// <summary>
    /// Main thread: makes a cell for every queued soldier that <paramref name="alive"/> accepts and that has none.
    /// A soldier removed between his queueing and this call is refused there, so no cell outlives him.
    /// </summary>
    public int Admit(float now, Func<TKey, bool> alive)
    {
        var admitted = 0;
        while (_pending.TryDequeue(out var key))
        {
            if (!alive(key))
                continue;
            var cell = new StaminaCell(now);
            if (!_cells.TryAdd(key, cell))
                continue;
            _roster.Add(new KeyValuePair<TKey, StaminaCell>(key, cell));
            admitted++;
        }

        return admitted;
    }

    /// <summary>Any thread.</summary>
    public bool TryGet(TKey key, out StaminaCell cell) => _cells.TryGetValue(key, out cell!);

    /// <summary>Any thread: readers stop finding the cell now, and the tick drops it from the roster on its next pass.</summary>
    public void MarkRemoved(TKey key)
    {
        if (_cells.TryRemove(key, out var cell))
            cell.MarkRemoved();
    }

    /// <summary>Main thread.</summary>
    public KeyValuePair<TKey, StaminaCell> At(int index) => _roster[index];

    /// <summary>Main thread: drops the entry by moving the last one into its place, so the caller looks at the same index again.</summary>
    public void RemoveAt(int index)
    {
        var entry = _roster[index];
        _cells.TryRemove(entry.Key, out _);
        var last = _roster.Count - 1;
        _roster[index] = _roster[last];
        _roster.RemoveAt(last);
    }

    /// <summary>Main thread, at a mission's first tick and at its end.</summary>
    public void Clear()
    {
        _cells.Clear();
        _roster.Clear();
        while (_pending.TryDequeue(out _))
        {
        }
    }
}
