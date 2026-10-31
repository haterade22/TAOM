using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// Builds a <see cref="MapLayout"/> on any open scene from two base points. Lane 0 lies on the negative side of the
/// base-to-base axis turned 90 degrees counter-clockwise; the last lane lies opposite it. Heights are interpolated
/// between the bases; the engine layer snaps them to the ground. Camps come out with an empty troop id and zero count
/// and reward: the engine layer fills those from maps.json.
/// </summary>
internal static class LaneLayoutGenerator
{
    // The bow reaches full width at this fraction of the lane length and holds until the mirror point.
    private const float BowFraction = 0.25f;
    private const float CampFraction = 0.35f;
    private const float DefaultWaypointStep = 12f;
    private const int DenseSamples = 400;
    private const float DirectionProbe = 1f;

    public static MapLayout Generate(string scene, Vec3f westBase, Vec3f eastBase, LayoutOptions options)
    {
        var laneCount = Math.Max(1, options.LaneCount);
        var step = options.WaypointStep > 0f ? options.WaypointStep : DefaultWaypointStep;

        var dx = eastBase.X - westBase.X;
        var dy = eastBase.Y - westBase.Y;
        var axisLength = (float)Math.Sqrt(dx * dx + dy * dy);
        var ux = axisLength > 1e-4f ? dx / axisLength : 1f;
        var uy = axisLength > 1e-4f ? dy / axisLength : 0f;

        var lanes = new List<LaneLayout>();
        for (var k = 0; k < laneCount; k++)
        {
            var offset = (k - (laneCount - 1) / 2f) * options.LaneSpread;
            lanes.Add(new LaneLayout(k, BuildPath(westBase, eastBase, -uy, ux, offset, step)));
        }

        var fractions = options.TowerFractions.Select(f => Math.Max(0f, Math.Min(1f, f)))
            .OrderByDescending(f => f).ToList();

        var towers = new List<TowerSlot>();
        foreach (var lane in lanes)
            AddLaneTowers(towers, lane, fractions);

        towers.Add(MakeKeep(MatchSide.West, westBase, eastBase, options.KeepInset, ux, uy));
        towers.Add(MakeKeep(MatchSide.East, eastBase, westBase, options.KeepInset, -ux, -uy));

        var points = new List<CapturePointSlot>();
        var camps = new List<CampSlot>();
        for (var i = 0; i + 1 < lanes.Count; i++)
        {
            points.Add(new CapturePointSlot("point_" + i, Between(lanes[i], lanes[i + 1], 0.5f)));
            camps.Add(new CampSlot("camp_west_" + i, Between(lanes[i], lanes[i + 1], CampFraction), "", 0, 0));
            camps.Add(new CampSlot("camp_east_" + i, Between(lanes[i], lanes[i + 1], 1f - CampFraction), "", 0, 0));
        }

        return new MapLayout(scene, lanes, towers, points, camps);
    }

    /// <summary>
    /// A smooth curve from base to base, resampled at equal arc length so no segment exceeds the step. The offset grows
    /// with a smoothstep over the first quarter of the lane, holds, and shrinks the same way into the other base.
    /// </summary>
    private static IReadOnlyList<Vec3f> BuildPath(Vec3f west, Vec3f east, float perpX, float perpY, float offset, float step)
    {
        var dense = new Vec3f[DenseSamples + 1];
        var cumulative = new float[DenseSamples + 1];
        for (var i = 0; i <= DenseSamples; i++)
        {
            var t = i / (float)DenseSamples;
            var lateral = offset * Bow(t);
            var along = Vec3f.Lerp(west, east, t);
            dense[i] = new Vec3f(along.X + perpX * lateral, along.Y + perpY * lateral, along.Z);
            if (i > 0)
                cumulative[i] = cumulative[i - 1] + dense[i].Distance2D(dense[i - 1]);
        }

        var arc = cumulative[DenseSamples];
        var segments = Math.Max(1, (int)Math.Ceiling(arc / step));
        var path = new Vec3f[segments + 1];
        path[0] = west;
        path[segments] = east;
        var cursor = 1;
        for (var i = 1; i < segments; i++)
        {
            var target = arc * i / segments;
            while (cursor < DenseSamples && cumulative[cursor] < target)
                cursor++;
            var span = cumulative[cursor] - cumulative[cursor - 1];
            var f = span <= 0f ? 0f : (target - cumulative[cursor - 1]) / span;
            path[i] = Vec3f.Lerp(dense[cursor - 1], dense[cursor], f);
        }

        return path;
    }

    private static float Bow(float t)
    {
        var x = Math.Min(t, 1f - t) / BowFraction;
        x = Math.Max(0f, Math.Min(1f, x));
        return x * x * (3f - 2f * x);
    }

    private static void AddLaneTowers(List<TowerSlot> towers, LaneLayout lane, IReadOnlyList<float> fractions)
    {
        var length = LaneGeometry.PathLength(lane.Path);
        for (var order = 0; order < fractions.Count; order++)
        {
            var arc = fractions[order] * length;
            towers.Add(MakeTower(lane, MatchSide.West, order, arc, length));
            towers.Add(MakeTower(lane, MatchSide.East, order, length - arc, length));
        }
    }

    private static TowerSlot MakeTower(LaneLayout lane, MatchSide owner, int order, float arc, float length)
    {
        var position = LaneGeometry.PointAt(lane.Path, arc);
        var ahead = LaneGeometry.PointAt(lane.Path, Math.Min(length, arc + DirectionProbe));
        var behind = LaneGeometry.PointAt(lane.Path, Math.Max(0f, arc - DirectionProbe));
        var march = owner == MatchSide.West ? ahead - behind : behind - ahead;
        var id = $"tower_{SideName(owner)}_{lane.Index}_{order}";
        return new TowerSlot(id, owner, lane.Index, order, TowerKind.Tower, position, Yaw(march.X, march.Y));
    }

    private static TowerSlot MakeKeep(MatchSide owner, Vec3f ownBase, Vec3f enemyBase, float inset, float ux, float uy)
    {
        var position = Vec3f.Lerp(ownBase, enemyBase, inset);
        return new TowerSlot("keep_" + SideName(owner), owner, -1, 99, TowerKind.Keep, position, Yaw(ux, uy));
    }

    /// <summary>The midpoint of two neighbour lanes, each taken at the same fraction of its own length from the West end.</summary>
    private static Vec3f Between(LaneLayout a, LaneLayout b, float fraction)
    {
        var pa = LaneGeometry.PointAt(a.Path, fraction * LaneGeometry.PathLength(a.Path));
        var pb = LaneGeometry.PointAt(b.Path, fraction * LaneGeometry.PathLength(b.Path));
        return Vec3f.Lerp(pa, pb, 0.5f);
    }

    private static float Yaw(float x, float y) => (float)Math.Atan2(y, x);

    private static string SideName(MatchSide side) => side == MatchSide.West ? "west" : "east";
}
