using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Features.FordsOfWar.Diagnostics;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.FordsOfWar;

/// <summary>
/// The 1 Hz look at the field for the run report: how far each stream has pushed, whether a held lane (its front unit within 10 m of
/// the hold point, measured along the lane's axis) stands inside a tower's fire and which tower covers it, how many enemy melee units
/// stand at each tower and how close anyone came to each capture point. Engine-free (it reads the units the battlefield adapter
/// already measured) but outside the pure diagnostics folder because it reads <see cref="FordsUnitSample"/>. Distances are 2D. No
/// LINQ and no allocation per unit.
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
        for (var i = 0; i < streams; i++)
        {
            holdFront[i] = float.MaxValue;
            holdFrontToPoint[i] = float.MaxValue;
            coverDistance[i] = float.MaxValue;
            coverIndex[i] = -1;
        }

        foreach (var pair in applied)
        {
            var (side, lane) = pair.Key;
            if (pair.Value.Kind == LaneCommandKind.HoldAt && lane >= 0 && lane < lanes)
            {
                holdAt[LaneStreams.Index(side, lane, lanes)] = pair.Value.Target;
                holdAxis[LaneStreams.Index(side, lane, lanes)] = AxisAt(layout, lane, pair.Value.Target);
            }
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
                if (d <= facts.FireRange)
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
                if (inRange)
                    inFire[stream]++;
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

        return new FordsFieldSample(LanesOf(lanes, live, inFire, front, holdFront, holdFrontToPoint, applied, standing, coverIndex),
            TowersOf(standing, meleeNear), PointsOf(points, nearest, view), liveBySide[0], liveBySide[1], backlog);
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

    private static List<FordsLaneSample> LanesOf(int lanes, int[] live, int[] inFire, float[] front, float[] holdFront, float[] holdFrontToPoint,
        IReadOnlyDictionary<(MatchSide, int), LaneCommand> applied, List<TowerSlot> standing, int[] coverIndex)
    {
        var result = new List<FordsLaneSample>(live.Length);
        for (var side = 0; side < LaneStreams.Sides; side++)
        {
            for (var lane = 0; lane < lanes; lane++)
            {
                var key = ((MatchSide)side, lane);
                var stream = LaneStreams.Index((MatchSide)side, lane, lanes);
                var holding = applied.TryGetValue(key, out var command) && command.Kind == LaneCommandKind.HoldAt;
                var settled = holding && holdFrontToPoint[stream] <= HoldArrivalMetres;
                var hold = settled && holdFront[stream] != float.MaxValue ? holdFront[stream] : -1f;
                var cover = coverIndex[stream] >= 0 ? standing[coverIndex[stream]].Id : null;
                result.Add(new FordsLaneSample((MatchSide)side, lane, live[stream], front[stream], settled, inFire[stream], hold, cover));
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
