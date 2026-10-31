using System.Collections.Generic;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// A bounded record of units that just died: their side and owner slot, kept only so a kill credited to a dead killer
/// (mutual kills, an arrow from a dead archer) can still pay its owner. The oldest entry goes first once the cap is full.
/// </summary>
internal sealed class RecentDeathLog
{
    public const int DefaultCapacity = 2048;

    private readonly int _capacity;
    private readonly Dictionary<AgentKey, LinkedListNode<Entry>> _byKey = new();
    private readonly LinkedList<Entry> _order = new();

    public RecentDeathLog(int capacity = DefaultCapacity)
    {
        _capacity = capacity < 1 ? 1 : capacity;
    }

    public int Count => _byKey.Count;

    /// <summary>Records a death. A key recorded again moves to the newest position.</summary>
    public void Record(AgentKey key, MatchSide side, int ownerSlot)
    {
        Forget(key);
        _byKey[key] = _order.AddLast(new Entry(key, side, ownerSlot));
        if (_byKey.Count <= _capacity)
            return;

        var oldest = _order.First!.Value;
        _order.RemoveFirst();
        _byKey.Remove(oldest.Key);
    }

    public bool TryGet(AgentKey key, out MatchSide side, out int ownerSlot)
    {
        if (_byKey.TryGetValue(key, out var node))
        {
            side = node.Value.Side;
            ownerSlot = node.Value.OwnerSlot;
            return true;
        }

        side = default;
        ownerSlot = -1;
        return false;
    }

    /// <summary>Drops a key, for example when the engine layer registers a live unit under it again.</summary>
    public void Forget(AgentKey key)
    {
        if (!_byKey.TryGetValue(key, out var node))
            return;

        _order.Remove(node);
        _byKey.Remove(key);
    }

    private readonly record struct Entry(AgentKey Key, MatchSide Side, int OwnerSlot);
}
