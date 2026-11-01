using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// What a lane formation does next. The checks run in this order and the first that applies wins: no units, fall back,
/// enemy within engage range (charge), next enemy tower in reach and open (attack it, unless holding), hold, march on.
/// A tower is in reach when the lead is within attack range of it or has come level with it along the lane, so a lead that
/// was carried past a standing tower (by a charge, say) attacks it instead of marching on.
/// Hold has one target that does not depend on where the formation stands, so a held lane is given the same order every tick.
/// The distances in the situation are measured from the formation's lead group, and the march keeps a waypoint index per
/// (side, lane) that only moves forward until the lane stops marching.
/// </summary>
internal sealed class LaneMarchRules
{
    private readonly FordsLaneConfig _lanes;
    private readonly FordsTowerConfig _towers;
    private readonly TowerChain _chain;
    private readonly Dictionary<int, LanePath> _paths;
    private readonly Dictionary<(MatchSide, int), int> _marchIndex = new();

    public LaneMarchRules(FordsLaneConfig lanes, FordsTowerConfig towers, MapLayout layout, TowerChain chain)
    {
        _lanes = lanes;
        _towers = towers;
        _chain = chain;
        _paths = new Dictionary<int, LanePath>();
        foreach (var lane in layout.Lanes)
            _paths[lane.Index] = new LanePath(lane.Path);
    }

    /// <summary>
    /// The lead group of a lane's unit positions on that lane's path (see <see cref="LanePath.LeadGroup"/>); null for an unknown
    /// lane, no readable position or a path under two points.
    /// </summary>
    public (Vec3f Position, float Arc, int Count)? LeadOf(MatchSide side, int lane, IReadOnlyList<Vec3f> positions) =>
        _paths.TryGetValue(lane, out var path) ? path.LeadGroup(side, positions, _lanes.LeadBand) : null;

    /// <summary>The command for the situation together with the branch that produced it and the numbers it was decided from.</summary>
    public LaneDecision Evaluate(LaneSituation s)
    {
        _paths.TryGetValue(s.Lane, out var path);
        var lead = s.Lead ?? s.Center;
        var leadArc = s.Lead != null ? s.LeadArc : s.Center is { } c && path != null ? path.ArcPosition(c) : 0f;
        var leadCount = s.Lead != null ? s.LeadCount : s.LiveUnits;

        var branch = LaneBranch.None;
        var command = LaneCommand.None;
        var waypoint = -1;
        if (s.LiveUnits > 0 && s.Center != null)
        {
            if (s.Order == LaneOrder.FallBack)
            {
                branch = LaneBranch.FallBack;
                command = FallBack(s, path);
            }
            else if (s.NearestEnemyDistance <= _lanes.EngageRange)
            {
                branch = LaneBranch.Charge;
                command = LaneCommand.Charge;
            }
            else if (s.Order != LaneOrder.Hold && s.NextEnemyTowerId != null && !s.NextEnemyTowerLocked
                && TowerInReach(s, path, leadArc))
            {
                branch = LaneBranch.AttackTower;
                command = LaneCommand.AttackTower(s.NextEnemyTowerId);
            }
            else if (s.Order == LaneOrder.Hold)
            {
                branch = LaneBranch.Hold;
                command = Hold(s, path);
            }
            else
            {
                branch = LaneBranch.March;
                (command, waypoint) = March(s, path, leadArc);
            }
        }

        if (branch != LaneBranch.March)
            _marchIndex.Remove((s.Side, s.Lane));

        return new LaneDecision(s.Side, s.Lane, branch, s.Order, s.LiveUnits, s.Center, lead, leadArc, leadCount,
            s.NearestEnemyDistance, s.NextEnemyTowerId, s.NextEnemyTowerDistance, s.CenterTowerDistance, s.NextEnemyTowerLocked,
            waypoint, command);
    }

    /// <summary>
    /// Walk to the first waypoint the lead has not reached. The index never steps back while the lane keeps marching, so a
    /// fresh wave at the base (which pulls the lead's neighbours back) cannot turn the formation around.
    /// </summary>
    private (LaneCommand Command, int Index) March(LaneSituation s, LanePath? path, float leadArc)
    {
        if (path == null || path.Points.Count == 0)
            return (LaneCommand.None, -1);

        var index = path.WaypointIndex(s.Side, leadArc, _lanes.WaypointReachRadius);
        var key = (s.Side, s.Lane);
        if (_marchIndex.TryGetValue(key, out var kept))
            index = s.Side == MatchSide.West ? Math.Max(kept, index) : Math.Min(kept, index);

        _marchIndex[key] = index;
        return (LaneCommand.MoveTo(path.Points[index]), index);
    }

    /// <summary>
    /// The next enemy tower is in reach when the lead is within attack range of it, or level with it or past it along the lane
    /// (West: lead arc at or above the tower's, East: at or below), because a lead carried past a standing tower is too far from
    /// it for the range test yet must not leave it behind.
    /// </summary>
    private bool TowerInReach(LaneSituation s, LanePath? path, float leadArc)
    {
        if (s.NextEnemyTowerDistance <= _towers.AttackRange)
            return true;

        var tower = _chain.Slot(s.NextEnemyTowerId!);
        if (tower == null || path == null)
            return false;

        var towerArc = path.ArcPosition(tower.Position);
        return s.Side == MatchSide.West ? leadArc >= towerArc : leadArc <= towerArc;
    }

    /// <summary>Retreat to the lane waypoint nearest the outermost standing own tower, else the keep, else the base end.</summary>
    private LaneCommand FallBack(LaneSituation s, LanePath? lanePath)
    {
        var path = lanePath?.Points;
        if (path == null || path.Count == 0)
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
    /// Hold HoldDistanceBeforeTower short of the next enemy tower, but never past the lane midpoint and at the midpoint when the
    /// next target is a keep (a lane that lost its towers must not hold on the enemy's doorstep): a lane short of the point
    /// advances to it, a lane past it falls back to it. The point is a path vertex on the holder's side. With no next enemy
    /// tower or no path for the lane there is no point to hold, so no command.
    /// </summary>
    private LaneCommand Hold(LaneSituation s, LanePath? path)
    {
        var tower = s.NextEnemyTowerId == null ? null : _chain.Slot(s.NextEnemyTowerId);
        if (tower == null || path == null)
            return LaneCommand.None;

        return LaneCommand.HoldAt(path.HoldPoint(tower.Position, s.Side, _lanes.HoldDistanceBeforeTower, tower.Kind == TowerKind.Keep));
    }
}
