using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// What a lane formation does next. The checks run in this order and the first that applies wins: no units, fall back,
/// siege (under Push, lead within fire range of the next enemy tower and local attackers there outnumber its defenders 1.5 to 1: attack the tower), enemy within
/// the side's charge range (charge; the engage range unless the faction's <see cref="LaneTactic"/> names another; a charging lane
/// keeps charging out to its close range), enemy within its close range (close on it as one formation), seize (go to the objective, and charge at a camp once a unit is in its alert range), next enemy tower in
/// reach and open (attack it, unless holding), hold, march on.
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
    private readonly Dictionary<(MatchSide, int), string> _siege = new();
    private readonly IReadOnlyDictionary<MatchSide, LaneTactic> _tactics;
    private readonly HashSet<(MatchSide, int)> _charging = new();
    private readonly Dictionary<(MatchSide, int), Vec3f> _closeTarget = new();

    /// <summary>
    /// Metres the nearest enemy must move from a closing lane's target before the target follows it (or the side's charge range,
    /// when that is smaller, so a lead that reaches the target is always within charge range of the enemy). The order then changes
    /// only when the enemy has moved, not on every tick or with every change of the ground's height.
    /// </summary>
    internal const float CloseRetargetDistance = 5f;

    /// <summary>Attackers per defender near the target tower that starts a siege.</summary>
    internal const float SiegeEnterRatio = 1.5f;

    /// <summary>Attackers per defender near the target tower below which a siege ends.</summary>
    internal const float SiegeStayRatio = 1.0f;

    /// <summary>
    /// A siege starts with the lead within the tower's FireRange of the target tower and lasts while the lead is within
    /// FireRange plus this margin of it. The gap between the two radii keeps a lead hovering at the exit line from flipping the
    /// command every tick.
    /// </summary>
    internal const float SiegeStayMargin = 15f;

    public LaneMarchRules(FordsLaneConfig lanes, FordsTowerConfig towers, MapLayout layout, TowerChain chain,
        IReadOnlyDictionary<MatchSide, LaneTactic>? tactics = null)
    {
        _lanes = lanes;
        _towers = towers;
        _chain = chain;
        _tactics = tactics ?? new Dictionary<MatchSide, LaneTactic>();
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
        var tactic = _tactics.TryGetValue(s.Side, out var own) ? own : LaneTactic.None;
        var key = (s.Side, s.Lane);
        // A charging lane keeps charging while the enemy stays inside its close range, so a melee does not flap back into the
        // close arrangement each time the nearest enemy falls. Without a close phase both radii are the charge range, as before.
        var chargeRange = _charging.Contains(key) ? tactic.CloseAt(_lanes.EngageRange) : tactic.ChargeAt(_lanes.EngageRange);

        var branch = LaneBranch.None;
        var command = LaneCommand.None;
        var waypoint = -1;
        var siege = false;
        if (s.LiveUnits > 0 && s.Center != null)
        {
            siege = UpdateSiege(s);
            if (s.Order == LaneOrder.FallBack)
            {
                branch = LaneBranch.FallBack;
                command = FallBack(s, path);
            }
            else if (siege)
            {
                branch = LaneBranch.AttackTower;
                command = LaneCommand.AttackTower(s.NextEnemyTowerId!);
            }
            else if (s.NearestEnemyDistance <= chargeRange)
            {
                branch = LaneBranch.Charge;
                command = LaneCommand.Charge;
            }
            else if (s.NearestEnemyDistance <= tactic.CloseAt(_lanes.EngageRange) && Close(s, path, leadArc, tactic) is { } close)
            {
                branch = LaneBranch.Close;
                command = close;
            }
            else if (s.Order == LaneOrder.Seize && s.Seize != null)
            {
                branch = LaneBranch.Seize;
                command = SeizeCommand(s, s.Seize);
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

        if (!siege)
            _siege.Remove((s.Side, s.Lane));

        if (branch == LaneBranch.Charge)
            _charging.Add(key);
        else
            _charging.Remove(key);

        if (branch == LaneBranch.Close && command.Kind == LaneCommandKind.MoveTo)
            _closeTarget[key] = command.Target;
        else
            _closeTarget.Remove(key);

        if (branch != LaneBranch.March)
            _marchIndex.Remove((s.Side, s.Lane));

        return new LaneDecision(s.Side, s.Lane, branch, s.Order, s.LiveUnits, s.Center, lead, leadArc, leadCount,
            s.NearestEnemyDistance, s.NextEnemyTowerId, s.NextEnemyTowerDistance, s.CenterTowerDistance, s.NextEnemyTowerLocked,
            waypoint, command, s.NearAttackers, s.NearDefenders, siege, s.Seize?.Id, tactic.ArrangementFor(branch));
    }

    /// <summary>
    /// Close on the nearest enemy as one formation: move to where it stands, ground it walked on, and keep that target until the
    /// enemy is more than <see cref="CloseRetargetDistance"/> from it. A held lane keeps its hold point and a seizing lane its
    /// objective; both only take the close arrangement. Null, so the next branch decides, when there is no enemy position, no path or
    /// no point to hold, when a seizing lane would charge a camp anyway, and when the lead is within attack range of the next enemy
    /// tower or the lead or the target is level with or past it along the lane: that tower comes first, and a Move past it is what
    /// check 9 fails.
    /// </summary>
    private LaneCommand? Close(LaneSituation s, LanePath? path, float leadArc, LaneTactic tactic)
    {
        if (s.Order == LaneOrder.Hold)
            return Hold(s, path) is { Kind: not LaneCommandKind.None } hold ? hold : null;

        if (s.Order == LaneOrder.Seize && s.Seize != null)
            return SeizeCommand(s, s.Seize) is { Kind: not LaneCommandKind.Charge } seize ? seize : null;

        if (path == null || s.NearestEnemy is not { } enemy || s.NextEnemyTowerDistance <= _towers.AttackRange)
            return null;

        var keep = Math.Min(CloseRetargetDistance, tactic.ChargeAt(_lanes.EngageRange));
        var target = _closeTarget.TryGetValue((s.Side, s.Lane), out var last) && last.Distance2D(enemy) <= keep ? last : enemy;
        if (s.NextEnemyTowerId != null && _chain.Slot(s.NextEnemyTowerId) is { } tower)
        {
            var towerArc = path.ArcPosition(tower.Position);
            if (LevelOrPast(s.Side, leadArc, towerArc) || LevelOrPast(s.Side, path.ArcPosition(target), towerArc))
                return null;
        }

        return LaneCommand.MoveTo(target);
    }

    /// <summary>Whether an arc along the lane (from the West end) is level with or past a tower's, seen from the side's own base.</summary>
    private static bool LevelOrPast(MatchSide side, float arc, float towerArc) => side == MatchSide.West ? arc >= towerArc : arc <= towerArc;

    /// <summary>Charge a camp once a unit is inside its alert range, else hold at the objective.</summary>
    private static LaneCommand SeizeCommand(LaneSituation s, SeizeTarget seize) =>
        seize.IsCamp && s.SeizeDistance <= CampRules.AlertRange ? LaneCommand.Charge : LaneCommand.HoldAt(seize.Position);

    /// <summary>
    /// Whether the lane is besieging its next enemy tower. Only under Push, with an open tower and known local counts. It starts
    /// when the lead is within the tower's FireRange of the target tower and the attackers near it are at least SiegeEnterRatio
    /// times the defenders; it lasts while attackers match defenders (SiegeStayRatio) and the lead is within FireRange +
    /// SiegeStayMargin of the tower. A NaN distance fails both. Entry is tighter than stay, so a lead hovering at the stay line
    /// cannot flip the command. A siege that just ended needs the full entry ratio on a later tick. The state is per (side, lane)
    /// and drops when the target tower changes.
    /// </summary>
    private bool UpdateSiege(LaneSituation s)
    {
        var key = (s.Side, s.Lane);
        var id = s.NextEnemyTowerId;
        if (s.Order != LaneOrder.Push || id == null || s.NextEnemyTowerLocked || _chain.IsDestroyed(id)
            || s.NearAttackers < 1 || s.NearDefenders < 0)
        {
            _siege.Remove(key);
            return false;
        }

        var range = _chain.FireRange(id);
        var withinStay = s.NextEnemyTowerDistance <= range + SiegeStayMargin;
        if (_siege.TryGetValue(key, out var held) && held == id)
        {
            if (s.NearAttackers >= SiegeStayRatio * s.NearDefenders && withinStay)
                return true;

            _siege.Remove(key);
            return false;
        }

        _siege.Remove(key);
        if (!(s.NextEnemyTowerDistance <= range) || s.NearAttackers < SiegeEnterRatio * s.NearDefenders)
            return false;

        _siege[key] = id;
        return true;
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

        return LevelOrPast(s.Side, leadArc, path.ArcPosition(tower.Position));
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
    /// Hold HoldDistanceBeforeTower short of the next enemy tower, plus what that tower reaches past the base range (a lane on Hold has no answer to fire), but never past the lane midpoint and at the midpoint when the
    /// next target is a keep (a lane that lost its towers must not hold on the enemy's doorstep): a lane short of the point
    /// advances to it, a lane past it falls back to it. The point is a path vertex on the holder's side. With no next enemy
    /// tower or no path for the lane there is no point to hold, so no command.
    /// </summary>
    private LaneCommand Hold(LaneSituation s, LanePath? path)
    {
        var tower = s.NextEnemyTowerId == null ? null : _chain.Slot(s.NextEnemyTowerId);
        if (tower == null || path == null)
            return LaneCommand.None;

        var distance = _lanes.HoldDistanceBeforeTower + _chain.FireRange(tower.Id) - _towers.FireRange;
        return LaneCommand.HoldAt(path.HoldPoint(tower.Position, s.Side, distance, tower.Kind == TowerKind.Keep));
    }
}
