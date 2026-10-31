using System;
using System.Collections.Generic;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// Walking a lane polyline. All lengths and projections work in the ground plane (heights are snapped to the terrain
/// later), and a lane runs from the West base (index 0) to the East base (last index).
/// </summary>
internal static class LaneGeometry
{
    private const float Epsilon = 1e-3f;

    public static float PathLength(IReadOnlyList<Vec3f> path)
    {
        var total = 0f;
        for (var i = 1; i < path.Count; i++)
            total += path[i].Distance2D(path[i - 1]);
        return total;
    }

    /// <summary>Where a point projects onto the lane: 0 at the West end, 1 at the East end.</summary>
    public static float Progress(IReadOnlyList<Vec3f> path, Vec3f point)
    {
        var total = PathLength(path);
        return total <= 0f ? 0f : ArcPosition(path, point) / total;
    }

    public static int NearestIndex(IReadOnlyList<Vec3f> path, Vec3f point)
    {
        var best = -1;
        var bestDistance = float.MaxValue;
        for (var i = 0; i < path.Count; i++)
        {
            var d = path[i].Distance2D(point);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = i;
            }
        }

        return best;
    }

    /// <summary>
    /// The waypoint a unit of <paramref name="side"/> should walk to: the first one ahead of its projection on the lane
    /// (toward the enemy base), or the one after that when it is already within <paramref name="reachRadius"/>.
    /// Clamps at the enemy end of the lane.
    /// </summary>
    public static Vec3f NextWaypoint(IReadOnlyList<Vec3f> path, MatchSide side, Vec3f position, float reachRadius)
    {
        if (path.Count == 0)
            return position;

        var arcs = Arcs(path);
        var s = ArcPosition(path, position);
        var last = path.Count - 1;
        int index;
        if (side == MatchSide.West)
        {
            index = last;
            for (var i = 0; i < path.Count; i++)
            {
                if (arcs[i] > s + Epsilon)
                {
                    index = i;
                    break;
                }
            }

            if (index < last && path[index].Distance2D(position) <= reachRadius)
                index++;
        }
        else
        {
            index = 0;
            for (var i = last; i >= 0; i--)
            {
                if (arcs[i] < s - Epsilon)
                {
                    index = i;
                    break;
                }
            }

            if (index > 0 && path[index].Distance2D(position) <= reachRadius)
                index--;
        }

        return path[index];
    }

    /// <summary>
    /// The lane point <paramref name="distance"/> metres of lane before a tower, seen from the side that attacks it:
    /// West attackers come from lower arc positions, East attackers from higher ones. Clamps at the lane ends.
    /// </summary>
    public static Vec3f PointBeforeTower(IReadOnlyList<Vec3f> path, Vec3f tower, MatchSide attacker, float distance)
    {
        var towerArc = ArcPosition(path, tower);
        return PointAt(path, attacker == MatchSide.West ? towerArc - distance : towerArc + distance);
    }

    /// <summary>
    /// The unit ground direction (West to East) of the lane segment closest to <paramref name="point"/>; the zero vector for a lane
    /// with no length.
    /// </summary>
    public static Vec3f DirectionAt(IReadOnlyList<Vec3f> path, Vec3f point)
    {
        if (path.Count < 2)
            return default;

        var arc = ArcPosition(path, point);
        var walked = 0f;
        var direction = default(Vec3f);
        for (var i = 1; i < path.Count; i++)
        {
            var dx = path[i].X - path[i - 1].X;
            var dy = path[i].Y - path[i - 1].Y;
            var length = (float)Math.Sqrt(dx * dx + dy * dy);
            if (length > 0f)
                direction = new Vec3f(dx / length, dy / length, 0f);
            walked += length;
            if (arc <= walked && length > 0f)
                break;
        }

        return direction;
    }

    /// <summary>The point <paramref name="arc"/> metres along the lane from its West end, clamped to the lane.</summary>
    public static Vec3f PointAt(IReadOnlyList<Vec3f> path, float arc)
    {
        if (path.Count == 0)
            return default;
        if (path.Count == 1 || arc <= 0f)
            return path[0];

        var remaining = arc;
        for (var i = 1; i < path.Count; i++)
        {
            var segment = path[i].Distance2D(path[i - 1]);
            if (remaining <= segment)
                return segment <= 0f ? path[i] : Vec3f.Lerp(path[i - 1], path[i], remaining / segment);
            remaining -= segment;
        }

        return path[path.Count - 1];
    }

    private static float[] Arcs(IReadOnlyList<Vec3f> path)
    {
        var arcs = new float[path.Count];
        for (var i = 1; i < path.Count; i++)
            arcs[i] = arcs[i - 1] + path[i].Distance2D(path[i - 1]);
        return arcs;
    }

    /// <summary>Arc length from the West end to the closest point of the lane; the first segment wins a tie.</summary>
    private static float ArcPosition(IReadOnlyList<Vec3f> path, Vec3f point)
    {
        if (path.Count < 2)
            return 0f;

        var bestDistance = float.MaxValue;
        var bestArc = 0f;
        var walked = 0f;
        for (var i = 1; i < path.Count; i++)
        {
            var a = path[i - 1];
            var b = path[i];
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var length = (float)Math.Sqrt(dx * dx + dy * dy);
            var t = length <= 0f ? 0f : ((point.X - a.X) * dx + (point.Y - a.Y) * dy) / (length * length);
            t = Math.Max(0f, Math.Min(1f, t));
            var px = a.X + dx * t;
            var py = a.Y + dy * t;
            var d = (float)Math.Sqrt((point.X - px) * (point.X - px) + (point.Y - py) * (point.Y - py));
            if (d < bestDistance - Epsilon * 0.01f)
            {
                bestDistance = d;
                bestArc = walked + length * t;
            }

            walked += length;
        }

        return bestArc;
    }
}
