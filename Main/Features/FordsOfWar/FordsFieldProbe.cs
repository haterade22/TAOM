using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Features.FordsOfWar.Diagnostics;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.FordsOfWar;

/// <summary>
/// The 1 Hz look at the field for the run report: how far each stream has pushed, whether a held lane (its front unit within 10 m of
/// the hold point, measured along the lane's axis; a Seize's hold at a capture point or camp is not a lane hold) stands inside a tower's fire and which tower covers it, how many enemy melee units
/// stand at each tower and how close anyone came to each capture point. Engine-free (it reads the units the battlefield adapter
/// already measured) but outside the pure diagnostics folder because it reads <see cref="FordsUnitSample"/>. Distances are 2D. No
/// LINQ and no allocation per unit. It also counts, per stream, the units the engine holds alarmed and, for a stream whose applied
/// command is an AttackTower order at a standing tower, the units flagged to attack an entity and the melee ones in contact (within
/// <see cref="FordsMatchFacts.ContactMetres"/>, 4 m for a barricade) with that tower, so a stream's ladder is measured at the tower it was ordered at and no other. For a stream whose applied
/// command is a MoveTo it also counts the units whose arc along the lane is more than 10 m past the stream's next standing enemy tower,
/// measured from the units themselves and not from the rules' lead, so check 9's <c>bypass</c> part can fail on a fault in the rules.
/// </summary>
internal static class FordsFieldProbe
{
    private const float HoldArrivalMetres = 10f;

    internal static FordsFieldSample Sample(IReadOnlyList<FordsUnitSample> units, IReadOnlyDictionary<AgentKey, UnitRecord> records,
        MapLayout layout, HashSet<string> fallen, IReadOnlyDictionary<(MatchSide, int), LaneCommand> applied,
        MatchView view, FordsMatchFacts facts, int backlog)
    {
        var lanes = facts.Lanes;
        var streams = LaneStreams.Count(lanes);
        var live = new int[streams];
        var inFire = new int[streams];
        var front = new float[streams];
        var holdFront = new float[streams];
        var holdFrontToPoint = new float[streams];
        var holdAt = new Vec3f?[streams];
        var holdAxis = new Vec3f[streams];
        var coverDistance = new float[streams];
        var coverIndex = new int[streams];
        var alarmed = new int[streams];
        var attack = new AttackTally(streams);
        var march = new MarchTally(streams);
        for (var i = 0; i < streams; i++)
        {
            holdFront[i] = float.MaxValue;
            holdFrontToPoint[i] = float.MaxValue;
            coverDistance[i] = float.MaxValue;
            coverIndex[i] = -1;
        }

        var keeps = new Vec3f[LaneStreams.Sides];
        var standing = new List<TowerSlot>();
        foreach (var slot in layout.Towers)
        {
            if (slot.Kind == TowerKind.Keep)
                keeps[(int)slot.Owner] = slot.Position;
            if (!fallen.Contains(slot.Id))
                standing.Add(slot);
        }

        foreach (var pair in applied)
        {
            var (side, lane) = pair.Key;
            if (lane < 0 || lane >= lanes)
                continue;

            var stream = LaneStreams.Index(side, lane, lanes);
            if (pair.Value.Kind == LaneCommandKind.HoldAt && !IsObjective(layout, pair.Value.Target))
            {
                holdAt[stream] = pair.Value.Target;
                holdAxis[stream] = AxisAt(layout, lane, pair.Value.Target);
            }
            else if (pair.Value.Kind == LaneCommandKind.AttackTower)
            {
                attack.Index[stream] = IndexOf(standing, pair.Value.TowerId);
            }
            else if (pair.Value.Kind == LaneCommandKind.MoveTo)
            {
                march.Aim(stream, layout, lane, side, standing);
            }
        }

        var meleeNear = new int[standing.Count];
        var points = layout.CapturePoints;
        var nearest = new float[points.Count];
        for (var i = 0; i < nearest.Length; i++)
            nearest[i] = float.MaxValue;

        var liveBySide = new int[LaneStreams.Sides];
        foreach (var unit in units)
        {
            var side = (int)unit.Side;
            liveBySide[side]++;
            var melee = records.TryGetValue(unit.Key, out var record) && IsMelee(record.Role);
            var counted = unit.Lane >= 0 && unit.Lane < lanes;
            var stream = LaneStreams.Index(unit.Side, unit.Lane, lanes);
            var inRange = false;
            for (var t = 0; t < standing.Count; t++)
            {
                var tower = standing[t];
                if ((int)tower.Owner == side)
                    continue;

                var d = unit.Position.Distance2D(tower.Position);
                if (melee && d <= facts.AttackRange)
                    meleeNear[t]++;

                if (!counted)
                    continue;
                if (d <= facts.FireRangeOf(tower.Owner))
                {
                    inRange = true;
                    if (d < coverDistance[stream])
                    {
                        coverDistance[stream] = d;
                        coverIndex[stream] = t;
                    }
                }

                if (d < holdFront[stream])
                    holdFront[stream] = d;
            }

            if (counted)
            {
                live[stream]++;
                if (unit.Alarmed)
                    alarmed[stream]++;
                if (inRange)
                    inFire[stream]++;
                if (attack.Index[stream] is var target and >= 0)
                {
                    if (unit.AttackingEntity)
                        attack.Flagged[stream]++;
                    if (melee && unit.Position.Distance2D(standing[target].Position) <= facts.ContactMetres)
                        attack.Contact[stream]++;
                }

                if (march.IsPast(stream, unit))
                    march.Past[stream]++;

                var away = unit.Position.Distance2D(keeps[side]);
                if (away > front[stream])
                {
                    front[stream] = away;
                    if (holdAt[stream] is { } hold)
                        holdFrontToPoint[stream] = AlongAxis(unit.Position, hold, holdAxis[stream]);
                }
            }

            for (var p = 0; p < nearest.Length; p++)
            {
                var d = unit.Position.Distance2D(points[p].Position);
                if (d < nearest[p])
                    nearest[p] = d;
            }
        }

        return new FordsFieldSample(LanesOf(lanes, live, inFire, front, holdFront, holdFrontToPoint, standing, coverIndex, alarmed, attack, march),
            TowersOf(standing, meleeNear), PointsOf(points, nearest, view), liveBySide[0], liveBySide[1], backlog);
    }

    /// <summary>Per stream marching on a standing enemy tower: the tower, its arc along the lane and the lane's path, and how many units stand past it.</summary>
    private sealed class MarchTally
    {
        private readonly TowerSlot?[] _tower;
        private readonly float[] _arc;
        private readonly IReadOnlyList<Vec3f>?[] _path;

        public MarchTally(int streams)
        {
            _tower = new TowerSlot?[streams];
            _arc = new float[streams];
            _path = new IReadOnlyList<Vec3f>?[streams];
            Past = new int[streams];
        }

        public int[] Past { get; }

        public string? TowerId(int stream) => _tower[stream]?.Id;

        /// <summary>
        /// Picks the stream's next standing enemy tower: the outermost standing tower of the other side in this lane, else that side's
        /// keep. A stream with no tower left, or whose lane has no path, is not judged.
        /// </summary>
        public void Aim(int stream, MapLayout layout, int lane, MatchSide side, List<TowerSlot> standing)
        {
            TowerSlot? next = null;
            TowerSlot? keep = null;
            foreach (var slot in standing)
            {
                if (slot.Owner == side)
                    continue;
                if (slot.Kind == TowerKind.Keep)
                    keep = slot;
                else if (slot.Lane == lane && (next == null || slot.Order < next.Order))
                    next = slot;
            }

            next ??= keep;
            var path = PathOf(layout, lane);
            if (next == null || path == null || path.Count < 2)
                return;

            _tower[stream] = next;
            _path[stream] = path;
            _arc[stream] = LaneGeometry.ArcPosition(path, next.Position);
        }

        public bool IsPast(int stream, FordsUnitSample unit)
        {
            var path = _path[stream];
            if (path == null || float.IsNaN(unit.Position.X) || float.IsNaN(unit.Position.Y))
                return false;

            return FordsMarchWatch.IsPast(unit.Side, LaneGeometry.ArcPosition(path, unit.Position), _arc[stream]);
        }

        private static IReadOnlyList<Vec3f>? PathOf(MapLayout layout, int lane)
        {
            foreach (var candidate in layout.Lanes)
            {
                if (candidate.Index == lane)
                    return candidate.Path;
            }

            return null;
        }
    }

    /// <summary>Per stream: the index in the standing list of the tower its AttackTower order names (-1 for none), and the counts measured at it.</summary>
    private sealed class AttackTally
    {
        public AttackTally(int streams)
        {
            Index = new int[streams];
            Flagged = new int[streams];
            Contact = new int[streams];
            for (var i = 0; i < streams; i++)
                Index[i] = -1;
        }

        public int[] Index { get; }
        public int[] Flagged { get; }
        public int[] Contact { get; }
    }

    private static int IndexOf(List<TowerSlot> standing, string? towerId)
    {
        for (var t = 0; t < standing.Count; t++)
        {
            if (standing[t].Id == towerId)
                return t;
        }

        return -1;
    }

    /// <summary>
    /// A lane on Seize holds at the capture point or camp itself (<see cref="LaneMarchRules"/>' Seize command takes the layout's own
    /// position, and the director records the rules' command, not a fallback waypoint), so an exact match is a Seize, not a lane hold.
    /// </summary>
    private static bool IsObjective(MapLayout layout, Vec3f target)
    {
        foreach (var point in layout.CapturePoints)
        {
            if (point.Position == target)
                return true;
        }

        foreach (var camp in layout.Camps)
        {
            if (camp.Position == target)
                return true;
        }

        return false;
    }

    private static bool IsMelee(UnitRole role) => role == UnitRole.Infantry || role == UnitRole.Spear || role == UnitRole.Cavalry;

    /// <summary>The lane's direction at the hold point: the segment of the lane's own path nearest to it. Zero when the lane has no path.</summary>
    private static Vec3f AxisAt(MapLayout layout, int lane, Vec3f hold)
    {
        foreach (var candidate in layout.Lanes)
        {
            if (candidate.Index == lane)
                return LaneGeometry.DirectionAt(candidate.Path, hold);
        }

        return default;
    }

    /// <summary>How far a unit is from the hold point along the lane's axis; the straight distance when there is no axis.</summary>
    private static float AlongAxis(Vec3f position, Vec3f hold, Vec3f axis)
    {
        if (axis.X == 0f && axis.Y == 0f)
            return position.Distance2D(hold);

        var along = (position.X - hold.X) * axis.X + (position.Y - hold.Y) * axis.Y;
        return along < 0f ? -along : along;
    }

    /// <summary>A stream is settled only when <see cref="Sample"/> measured it against a lane hold: holdFrontToPoint stays at MaxValue otherwise.</summary>
    private static List<FordsLaneSample> LanesOf(int lanes, int[] live, int[] inFire, float[] front, float[] holdFront, float[] holdFrontToPoint,
        List<TowerSlot> standing, int[] coverIndex, int[] alarmed, AttackTally attack, MarchTally march)
    {
        var result = new List<FordsLaneSample>(live.Length);
        for (var side = 0; side < LaneStreams.Sides; side++)
        {
            for (var lane = 0; lane < lanes; lane++)
            {
                var stream = LaneStreams.Index((MatchSide)side, lane, lanes);
                var settled = holdFrontToPoint[stream] <= HoldArrivalMetres;
                var hold = settled && holdFront[stream] != float.MaxValue ? holdFront[stream] : -1f;
                var cover = coverIndex[stream] >= 0 ? standing[coverIndex[stream]].Id : null;
                var target = attack.Index[stream] >= 0 ? standing[attack.Index[stream]].Id : null;
                result.Add(new FordsLaneSample((MatchSide)side, lane, live[stream], front[stream], settled, inFire[stream], hold, cover, alarmed[stream],
                    target, attack.Flagged[stream], attack.Contact[stream], march.TowerId(stream), march.Past[stream]));
            }
        }

        return result;
    }

    private static List<FordsTowerSample> TowersOf(List<TowerSlot> standing, int[] meleeNear)
    {
        var result = new List<FordsTowerSample>(standing.Count);
        for (var t = 0; t < standing.Count; t++)
            result.Add(new FordsTowerSample(standing[t].Id, meleeNear[t]));
        return result;
    }

    private static List<FordsPointSample> PointsOf(IReadOnlyList<CapturePointSlot> points, float[] nearest, MatchView view)
    {
        var result = new List<FordsPointSample>(points.Count);
        for (var p = 0; p < points.Count; p++)
        {
            var contested = false;
            foreach (var v in view.CapturePoints)
            {
                if (v.PointId == points[p].Id)
                    contested = v.Contested;
            }

            result.Add(new FordsPointSample(points[p].Id, contested, nearest[p] == float.MaxValue ? -1f : nearest[p]));
        }

        return result;
    }
}
