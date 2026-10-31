using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// Hit points and lock state of every tower and keep. A tower is locked while a tower of the same owner and lane with a
/// lower Order stands. A keep is locked until its owner has lost every tower in at least one lane (a lane with no towers counts as lost).
/// This is the rules' mirror of the entities: the engine layer reports damage and destruction into it.
/// </summary>
internal sealed class TowerChain
{
    private sealed class TowerState
    {
        public TowerState(TowerSlot slot, int maxHitPoints)
        {
            Slot = slot;
            MaxHitPoints = maxHitPoints;
            HitPoints = maxHitPoints;
        }

        public TowerSlot Slot { get; }
        public int MaxHitPoints { get; }
        public int HitPoints { get; set; }
        public bool Destroyed { get; set; }
    }

    private readonly Dictionary<string, TowerState> _states = new();
    private readonly List<string> _ids = new();
    private readonly HashSet<int> _lanes = new();

    public TowerChain(MapLayout layout, FordsTowerConfig config)
    {
        foreach (var lane in layout.Lanes)
            _lanes.Add(lane.Index);

        foreach (var slot in layout.Towers)
        {
            if (_states.ContainsKey(slot.Id))
                continue;

            _states[slot.Id] = new TowerState(slot, MaxFor(slot, config));
            _ids.Add(slot.Id);
            if (slot.Kind == TowerKind.Tower)
                _lanes.Add(slot.Lane);
        }
    }

    public IReadOnlyList<string> Ids => _ids;

    public bool IsTower(string towerId) => _states.ContainsKey(towerId);

    public TowerSlot? Slot(string towerId) => _states.TryGetValue(towerId, out var s) ? s.Slot : null;

    public int MaxHitPoints(string towerId) => _states.TryGetValue(towerId, out var s) ? s.MaxHitPoints : 0;

    public int HitPoints(string towerId) => _states.TryGetValue(towerId, out var s) ? s.HitPoints : 0;

    public bool IsDestroyed(string towerId) => _states.TryGetValue(towerId, out var s) && s.Destroyed;

    /// <summary>True when the tower cannot be damaged yet. An unknown id counts as locked.</summary>
    public bool IsLocked(string towerId)
    {
        if (!_states.TryGetValue(towerId, out var tower))
            return true;

        var slot = tower.Slot;
        if (slot.Kind == TowerKind.Keep)
            return !OwnerLostALane(slot.Owner);

        return _states.Values.Any(o => !o.Destroyed
            && o.Slot.Kind == TowerKind.Tower
            && o.Slot.Owner == slot.Owner
            && o.Slot.Lane == slot.Lane
            && o.Slot.Order < slot.Order);
    }

    /// <summary>The standing towers (keeps excluded) of a side in a lane, outermost first.</summary>
    public IReadOnlyList<TowerSlot> AliveTowers(MatchSide owner, int lane) =>
        _states.Values
            .Where(s => !s.Destroyed && s.Slot.Kind == TowerKind.Tower && s.Slot.Owner == owner && s.Slot.Lane == lane)
            .Select(s => s.Slot)
            .OrderBy(s => s.Order)
            .ToList();

    /// <summary>Takes hit points off and returns what is left. Reaching zero destroys the tower.</summary>
    public int ApplyDamage(string towerId, int amount)
    {
        if (!_states.TryGetValue(towerId, out var tower) || tower.Destroyed)
            return 0;
        if (amount <= 0)
            return tower.HitPoints;

        tower.HitPoints = Math.Max(0, tower.HitPoints - amount);
        if (tower.HitPoints == 0)
            tower.Destroyed = true;
        return tower.HitPoints;
    }

    /// <summary>Restores hit points up to the maximum and returns how many were actually added.</summary>
    public int Heal(string towerId, int amount)
    {
        if (amount <= 0 || !_states.TryGetValue(towerId, out var tower) || tower.Destroyed)
            return 0;

        var next = Math.Min(tower.MaxHitPoints, tower.HitPoints + amount);
        var added = next - tower.HitPoints;
        tower.HitPoints = next;
        return added;
    }

    /// <summary>Marks the tower destroyed; true only the first time, so a repeated engine callback does nothing.</summary>
    public bool OnDestroyed(string towerId)
    {
        if (!_states.TryGetValue(towerId, out var tower) || tower.Destroyed)
            return false;

        tower.HitPoints = 0;
        tower.Destroyed = true;
        return true;
    }

    // A lane of the layout where the owner has no tower standing counts as lost, also when it never had one (the fitter or the
    // director can drop them all): its units already march at the keep.
    private bool OwnerLostALane(MatchSide owner)
    {
        var standing = _states.Values
            .Where(s => s.Slot.Kind == TowerKind.Tower && s.Slot.Owner == owner && !s.Destroyed)
            .Select(s => s.Slot.Lane)
            .ToHashSet();

        // With no lanes and no towers at all nothing stands in front of the keep.
        return _lanes.Count == 0 || _lanes.Any(lane => !standing.Contains(lane));
    }

    private static int MaxFor(TowerSlot slot, FordsTowerConfig config) =>
        slot.Kind == TowerKind.Keep ? config.KeepHitPoints
        : slot.Order == 0 ? config.TowerHitPoints
        : config.InnerTowerHitPoints;
}
