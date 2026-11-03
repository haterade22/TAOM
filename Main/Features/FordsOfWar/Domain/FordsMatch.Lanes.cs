using System;
using System.Collections.Generic;

namespace TAOM.Features.FordsOfWar.Domain;

internal sealed partial class FordsMatch
{
    private IReadOnlyList<LaneDecision> _decisions = Array.Empty<LaneDecision>();

    /// <summary>
    /// The decision for every lane of both sides from the latest tick that reached the lane step, in lane then side order; a
    /// lane with no units has Branch None. Empty before the first live tick and after a tick that ended early (countdown, match over).
    /// </summary>
    internal IReadOnlyList<LaneDecision> LastDecisions => _decisions;

    private void IssueLaneCommands(MatchEffects effects)
    {
        foreach (var (side, lane) in _seize.TakeFinished())
            _orders.Release(side, lane);

        var decisions = new List<LaneDecision>();
        foreach (var lane in _layout.Lanes)
        {
            foreach (var side in Sides)
            {
                _seenLanes.TryGetValue((side, lane.Index), out var seen);
                var decision = _march.Evaluate(SituationOf(side, lane.Index, seen));
                decisions.Add(decision);
                if (decision.Command.Kind != LaneCommandKind.None)
                    effects.Commands.Add(new FormationCommand(side, lane.Index, decision.Command));
            }
        }

        _decisions = decisions;
    }

    /// <summary>
    /// The tower and enemy distances are measured from the lane's lead group, so a fresh wave at the base cannot pull the test
    /// away from the front. A lane the engine reported nothing for (or with no centre) is an empty one.
    /// </summary>
    private LaneSituation SituationOf(MatchSide side, int lane, LaneSnapshot? seen)
    {
        var order = _orders.Order(side, lane);
        if (seen == null || seen.LiveUnits <= 0 || seen.Center is not { } center)
            return new LaneSituation(side, lane, order, 0, null, float.MaxValue, null, float.MaxValue, false);

        var target = NextTargetOf(MatchSides.Opponent(side), lane);
        var lead = seen.UnitPositions == null ? null : _march.LeadOf(side, lane, seen.UnitPositions);
        var from = lead?.Position ?? center;
        var distance = target == null ? float.MaxValue : from.Distance2D(target.Position);
        var centerDistance = target == null ? float.MaxValue : center.Distance2D(target.Position);
        // A lane fights its own lane's enemies: a neighbour's wave across the gap must not pull it out of its lane. At the keep the lanes meet.
        var seize = order == LaneOrder.Seize ? _seize.TargetOf(side, lane) : null;
        var anyLane = target == null || target.Kind == TowerKind.Keep || seize != null;
        var enemy = lead is { } found ? NearestEnemyFrom(side, lane, anyLane, found.Position, seen.NearestEnemyDistance) : seen.NearestEnemyDistance;
        var attackers = target == null ? -1 : UnitsNear(side, true, target);
        var defenders = target == null ? -1 : UnitsNear(side, false, target);

        return new LaneSituation(side, lane, order, seen.LiveUnits, center, enemy, target?.Id, distance,
            target == null || _chain.IsLocked(target.Id), lead?.Position, lead?.Arc ?? 0f, lead?.Count ?? 0, centerDistance,
            attackers, defenders, seize, seize == null ? float.MaxValue : NearestUnitTo(seize.Position, seen, center));
    }

    private static float NearestUnitTo(Vec3f point, LaneSnapshot lane, Vec3f center)
    {
        if (lane.UnitPositions == null || lane.UnitPositions.Count == 0)
            return center.Distance2D(point);

        var best = float.MaxValue;
        foreach (var position in lane.UnitPositions)
        {
            var distance = position.Distance2D(point);
            if (distance < best)
                best = distance;
        }

        return best;
    }

    /// <summary>
    /// How many units are within the fire range of <paramref name="tower"/>: the side's own units from lanes whose next enemy
    /// target is this same tower (<paramref name="own"/>, so a neighbouring lane's wave walking past does not count), or the other
    /// side's units from every lane. -1 when a lane of either side reported no positions (not known).
    /// </summary>
    private int UnitsNear(MatchSide side, bool own, TowerSlot tower)
    {
        var count = 0;
        foreach (var lane in _seenLanes.Values)
        {
            if (lane.UnitPositions == null)
                return -1;

            if ((lane.Side == side) != own)
                continue;

            if (own && NextTargetOf(MatchSides.Opponent(side), lane.Lane)?.Id != tower.Id)
                continue;

            foreach (var position in lane.UnitPositions)
            {
                if (position.Distance2D(tower.Position) <= _chain.FireRange(tower.Id))
                    count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Distance from <paramref name="point"/> to the nearest unit of the other side in <paramref name="ownLane"/> (every lane when
    /// <paramref name="anyLane"/>), from the unit positions of this tick's lanes.
    /// <paramref name="fallback"/> (the engine's centre-based figure) stands in when no enemy position was reported.
    /// </summary>
    private float NearestEnemyFrom(MatchSide side, int ownLane, bool anyLane, Vec3f point, float fallback)
    {
        var best = float.MaxValue;
        var any = false;
        foreach (var lane in _seenLanes.Values)
        {
            if (lane.Side == side || lane.UnitPositions == null)
                continue;

            any = true;
            if (!anyLane && lane.Lane != ownLane)
                continue;

            foreach (var position in lane.UnitPositions)
            {
                var distance = position.Distance2D(point);
                if (distance < best)
                    best = distance;
            }
        }

        return any ? best : fallback;
    }
}
