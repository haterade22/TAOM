using System.Collections.Generic;

namespace TAOM.Features.MixedFormations.Models;

/// <summary>
/// Cached per-formation slot map. Records (row, file) for every assigned unit, which of them are
/// ranged, the next-index counters for "new agent appears mid-mission" assignments, and the slots
/// deleted units vacated. A vacated slot goes to the next unit of the same class before any counter
/// advances, so a replacement stands where its predecessor did and the footprint stays the initial
/// one (#595, Codex review 109: dropping the mapping alone let every casualty's replacement take a
/// fresh counter value, a row deeper each time and on top of the other class's rows).
/// </summary>
public sealed class SlotAssignment
{
    public FormationLayoutType Layout { get; }
    public int FilesPerRow { get; }
    public Dictionary<int, (int row, int file)> ByAgentIndex { get; } = new();
    public int NextMeleeIndex { get; set; }
    public int NextRangedIndex { get; set; }

    private readonly HashSet<int> _rangedIndices = new();
    private readonly Stack<(int row, int file)> _freedMeleeSlots = new();
    private readonly Stack<(int row, int file)> _freedRangedSlots = new();

    public SlotAssignment(FormationLayoutType layout, int filesPerRow)
    {
        Layout = layout;
        FilesPerRow = filesPerRow;
    }

    /// <summary>Vacated melee slots waiting for the next melee unit.</summary>
    public int FreedMeleeCount => _freedMeleeSlots.Count;

    /// <summary>Vacated ranged slots waiting for the next ranged unit.</summary>
    public int FreedRangedCount => _freedRangedSlots.Count;

    /// <summary>Record <paramref name="slot"/> for <paramref name="agentIndex"/> and remember its class.</summary>
    public void Assign(int agentIndex, bool isRanged, (int row, int file) slot)
    {
        ByAgentIndex[agentIndex] = slot;
        if (isRanged) _rangedIndices.Add(agentIndex);
        else _rangedIndices.Remove(agentIndex);
    }

    /// <summary>
    /// Drop a deleted unit's slot and hold it for the next unit of its class. False when the index
    /// was never assigned here.
    /// </summary>
    public bool Forget(int agentIndex)
    {
        if (!ByAgentIndex.TryGetValue(agentIndex, out var slot)) return false;
        ByAgentIndex.Remove(agentIndex);
        if (_rangedIndices.Remove(agentIndex)) _freedRangedSlots.Push(slot);
        else _freedMeleeSlots.Push(slot);
        return true;
    }

    /// <summary>A vacated slot of the unit's class, most recently freed first; false when none waits.</summary>
    public bool TryReclaim(bool isRanged, out (int row, int file) slot)
    {
        var freed = isRanged ? _freedRangedSlots : _freedMeleeSlots;
        if (freed.Count == 0)
        {
            slot = default;
            return false;
        }
        slot = freed.Pop();
        return true;
    }
}
