using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// Neutral camps: how many units each still has, whether they are out fighting, and who is paid when the last one falls. The
/// reward goes to the slot of the clearing side that owns the lane nearest the camp. A camp is cleared once and does not come back.
/// </summary>
internal sealed class CampRules
{
    private sealed class Camp
    {
        public Camp(CampSlot slot, int nearestLane)
        {
            Slot = slot;
            NearestLane = nearestLane;
            Alive = Math.Max(0, slot.Count);
            Active = Alive > 0;
        }

        public CampSlot Slot { get; }
        public int NearestLane { get; }
        public int Alive { get; set; }
        public bool Active { get; }
        public bool Cleared { get; set; }
        public bool Alerted { get; set; }
    }

    /// <summary>How far the ring round a camp reaches from its centre, in metres.</summary>
    internal const float Radius = 15f;

    /// <summary>A unit this close to a camp's centre brings its creatures out; they go home when no unit is left this close.</summary>
    internal const float AlertRange = 20f;

    private readonly Func<MatchSide, int, int> _laneOwner;
    private readonly Dictionary<string, Camp> _camps = new();

    /// <param name="laneOwner">The commander slot that owns (side, lane).</param>
    public CampRules(MapLayout layout, Func<MatchSide, int, int> laneOwner)
    {
        _laneOwner = laneOwner;
        foreach (var slot in layout.Camps)
            _camps[slot.Id] = new Camp(slot, FindNearestLane(layout, slot.Position));
    }

    /// <summary>Units still alive in the camp; 0 for an unknown camp.</summary>
    public int Alive(string campId) => _camps.TryGetValue(campId, out var camp) ? camp.Alive : 0;

    public bool IsCleared(string campId) => _camps.TryGetValue(campId, out var camp) && camp.Cleared;

    /// <summary>Whether the camp has units left to fight.</summary>
    public bool IsOpen(string campId) => _camps.TryGetValue(campId, out var camp) && camp.Active && !camp.Cleared;

    /// <summary>The camps whose creatures change between out and home, given where every lane's units stand.</summary>
    public IReadOnlyList<CampOrder> UpdateAlerts(IReadOnlyList<LaneSnapshot> lanes)
    {
        var orders = new List<CampOrder>();
        foreach (var camp in _camps.Values)
        {
            if (!camp.Active || camp.Cleared)
                continue;

            var alerted = lanes.Any(lane => HasUnitNear(lane, camp.Slot.Position));
            if (alerted != camp.Alerted)
            {
                camp.Alerted = alerted;
                orders.Add(new CampOrder(camp.Slot.Id, alerted));
            }
        }

        return orders;
    }

    /// <summary>The lane closest to the camp (a tie goes to the lower index); -1 for an unknown camp or a map without lanes.</summary>
    public int NearestLane(string campId) => _camps.TryGetValue(campId, out var camp) ? camp.NearestLane : -1;

    /// <summary>
    /// A camp unit died. Returns the cleared event when it was the last one, otherwise null. A camp that never had units,
    /// an unknown camp and an already cleared camp are ignored.
    /// </summary>
    public CampCleared? OnUnitKilled(string campId, MatchSide? killerSide)
    {
        if (!_camps.TryGetValue(campId, out var camp) || !camp.Active || camp.Cleared)
            return null;

        camp.Alive = Math.Max(0, camp.Alive - 1);
        if (camp.Alive > 0)
            return null;

        camp.Cleared = true;
        if (killerSide == null || camp.NearestLane < 0)
            return new CampCleared(campId, killerSide, -1, 0);

        return new CampCleared(campId, killerSide, _laneOwner(killerSide.Value, camp.NearestLane), camp.Slot.Reward);
    }

    private static bool HasUnitNear(LaneSnapshot lane, Vec3f point)
    {
        if (lane.UnitPositions != null)
            return lane.UnitPositions.Any(p => p.Distance2D(point) <= AlertRange);

        return lane.LiveUnits > 0 && lane.Center is { } center && center.Distance2D(point) <= AlertRange;
    }

    private static int FindNearestLane(MapLayout layout, Vec3f position)
    {
        var best = -1;
        var bestDistance = float.MaxValue;
        foreach (var lane in layout.Lanes)
        {
            var index = LaneGeometry.NearestIndex(lane.Path, position);
            if (index < 0)
                continue;

            var distance = lane.Path[index].Distance2D(position);
            if (distance < bestDistance - 1e-4f)
            {
                bestDistance = distance;
                best = lane.Index;
            }
        }

        return best;
    }
}
