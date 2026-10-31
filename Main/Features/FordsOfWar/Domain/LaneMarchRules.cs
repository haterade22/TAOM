using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// What a lane formation does next. The checks run in this order and the first that applies wins: no units, fall back,
/// enemy within engage range (charge), next enemy tower in reach and open (attack it, unless holding), hold, march on.
/// Hold has one target that does not depend on where the formation stands, so a held lane is given the same order every tick.
/// </summary>
internal sealed class LaneMarchRules
{
    private readonly FordsLaneConfig _lanes;
    private readonly FordsTowerConfig _towers;
    private readonly TowerChain _chain;
    private readonly Dictionary<int, IReadOnlyList<Vec3f>> _paths;

    public LaneMarchRules(FordsLaneConfig lanes, FordsTowerConfig towers, MapLayout layout, TowerChain chain)
    {
        _lanes = lanes;
        _towers = towers;
        _chain = chain;
        _paths = new Dictionary<int, IReadOnlyList<Vec3f>>();
        foreach (var lane in layout.Lanes)
            _paths[lane.Index] = lane.Path;
    }

    public LaneCommand Decide(LaneSituation s)
    {
        if (s.LiveUnits <= 0 || s.Center is not { } center)
            return LaneCommand.None;

        if (s.Order == LaneOrder.FallBack)
            return FallBack(s);

        if (s.NearestEnemyDistance <= _lanes.EngageRange)
            return LaneCommand.Charge;

        if (s.Order != LaneOrder.Hold && s.NextEnemyTowerId != null
            && s.NextEnemyTowerDistance <= _towers.AttackRange && !s.NextEnemyTowerLocked)
            return LaneCommand.AttackTower(s.NextEnemyTowerId);

        if (s.Order == LaneOrder.Hold)
            return Hold(s);

        if (!_paths.TryGetValue(s.Lane, out var path))
            return LaneCommand.None;

        return LaneCommand.MoveTo(LaneGeometry.NextWaypoint(path, s.Side, center, _lanes.WaypointReachRadius));
    }

    /// <summary>Retreat to the lane waypoint nearest the outermost standing own tower, else the keep, else the base end.</summary>
    private LaneCommand FallBack(LaneSituation s)
    {
        if (!_paths.TryGetValue(s.Lane, out var path) || path.Count == 0)
            return LaneCommand.None;

        var anchor = _chain.AliveTowers(s.Side, s.Lane).FirstOrDefault()?.Position ?? StandingKeep(s.Side);
        if (anchor is not { } point)
            return LaneCommand.MoveTo(s.Side == MatchSide.West ? path[0] : path[path.Count - 1]);

        return LaneCommand.MoveTo(path[LaneGeometry.NearestIndex(path, point)]);
    }

    private Vec3f? StandingKeep(MatchSide side)
    {
        foreach (var id in _chain.Ids)
        {
            var slot = _chain.Slot(id);
            if (slot != null && slot.Kind == TowerKind.Keep && slot.Owner == side && !_chain.IsDestroyed(id))
                return slot.Position;
        }

        return null;
    }

    /// <summary>
    /// Hold at the lane point HoldDistanceBeforeTower short of the next enemy tower: a lane short of it advances to it, a lane
    /// past it falls back to it. With no next enemy tower or no path for the lane there is no point to hold, so no command.
    /// </summary>
    private LaneCommand Hold(LaneSituation s)
    {
        var tower = s.NextEnemyTowerId == null ? null : _chain.Slot(s.NextEnemyTowerId);
        if (tower == null || !_paths.TryGetValue(s.Lane, out var path))
            return LaneCommand.None;

        return LaneCommand.HoldAt(LaneGeometry.PointBeforeTower(path, tower.Position, s.Side, _lanes.HoldDistanceBeforeTower));
    }
}
