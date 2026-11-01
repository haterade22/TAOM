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
        var enemy = lead is { } found ? NearestEnemyFrom(side, found.Position, seen.NearestEnemyDistance) : seen.NearestEnemyDistance;
        return new LaneSituation(side, lane, order, seen.LiveUnits, center, enemy, target?.Id, distance,
            target == null || _chain.IsLocked(target.Id), lead?.Position, lead?.Arc ?? 0f, lead?.Count ?? 0, centerDistance);
    }

    /// <summary>
    /// Distance from <paramref name="point"/> to the nearest unit of the other side, from the unit positions of this tick's lanes.
    /// <paramref name="fallback"/> (the engine's centre-based figure) stands in when no enemy position was reported.
    /// </summary>
    private float NearestEnemyFrom(MatchSide side, Vec3f point, float fallback)
    {
        var best = float.MaxValue;
        var any = false;
        foreach (var lane in _seenLanes.Values)
        {
            if (lane.Side == side || lane.UnitPositions == null)
                continue;

            any = true;
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
