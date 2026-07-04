using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.WarOfTheRingMomentum.Domain;

/// <summary>
/// Per-side momentum state: enrolled kingdom StringIds, time-limited event queues
/// (capped per type), the side's accumulated momentum (×100 internal scale), and
/// running total stats. Pure data + math — no TaleWorlds types (ADR-007).
/// LOTRAOM parity source: WarOfTheRingFactionData.
/// </summary>
public class MomentumSideData
{
    public const int MaxEventsPerType = 100;

    private readonly List<string> _kingdomIds = new();
    private readonly Dictionary<MomentumActionType, Queue<MomentumEvent>> _events = new();

    public MomentumSideData()
    {
        foreach (MomentumActionType type in Enum.GetValues(typeof(MomentumActionType)))
        {
            _events[type] = new Queue<MomentumEvent>();
        }
    }

    /// <summary>Side momentum on the ×100 internal scale. Can go negative (decay).</summary>
    public int SideMomentum { get; private set; }

    public MomentumTotalStats TotalStats { get; } = new();

    public IReadOnlyList<string> KingdomIds => _kingdomIds;

    public bool ContainsKingdom(string kingdomId)
    {
        return !string.IsNullOrEmpty(kingdomId) && _kingdomIds.Contains(kingdomId);
    }

    public bool AddKingdom(string kingdomId)
    {
        if (string.IsNullOrEmpty(kingdomId) || _kingdomIds.Contains(kingdomId))
            return false;
        _kingdomIds.Add(kingdomId);
        return true;
    }

    public bool RemoveKingdom(string kingdomId)
    {
        return !string.IsNullOrEmpty(kingdomId) && _kingdomIds.Remove(kingdomId);
    }

    public void AddEvent(MomentumEvent momentumEvent)
    {
        if (momentumEvent == null) return;

        var queue = _events[momentumEvent.Type];
        // LOTRAOM parity: trim at cap withOUT subtracting the trimmed value — its
        // contribution becomes permanent (it can no longer decay either).
        while (queue.Count >= MaxEventsPerType)
            queue.Dequeue();

        queue.Enqueue(momentumEvent);
        EditMomentum(momentumEvent.Value);
    }

    public void EditMomentum(int amount)
    {
        SideMomentum += amount;
    }

    /// <summary>
    /// Decay pass: dequeues every event whose EndTimeHours has passed (strict
    /// <c>&lt; nowHours</c>, LOTRAOM parity) and subtracts its value.
    /// Returns the (non-positive) momentum change applied.
    /// </summary>
    public int ProcessExpiredEvents(double nowHours)
    {
        int momentumChange = 0;

        foreach (var queue in _events.Values)
        {
            while (queue.Count > 0 && queue.Peek().EndTimeHours < nowHours)
            {
                momentumChange -= queue.Dequeue().Value;
            }
        }

        if (momentumChange != 0)
            EditMomentum(momentumChange);

        return momentumChange;
    }

    public IEnumerable<MomentumEvent> GetEvents(MomentumActionType type)
    {
        return _events[type].ToList();
    }

    /// <summary>Save-load rehydration: set momentum without touching queues.</summary>
    public void RestoreMomentum(int momentum)
    {
        SideMomentum = momentum;
    }

    /// <summary>Save-load rehydration: enqueue without changing momentum (cap enforced).</summary>
    public void RestoreEvent(MomentumEvent momentumEvent)
    {
        if (momentumEvent == null) return;

        var queue = _events[momentumEvent.Type];
        while (queue.Count >= MaxEventsPerType)
            queue.Dequeue();

        queue.Enqueue(momentumEvent);
    }
}
