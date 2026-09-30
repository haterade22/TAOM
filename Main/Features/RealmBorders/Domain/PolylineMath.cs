using System;
using System.Collections.Generic;

namespace TAOM.Features.RealmBorders.Domain;

/// <summary>
/// Turns a border's grid stair-steps into a drawn curve: Ramer-Douglas-Peucker simplification first
/// (steps become straight runs), then Chaikin corner cutting (runs become curves). Open lines keep
/// both endpoints, so borders still meet exactly at three-realm corners.
/// </summary>
public static class PolylineMath
{
    /// <summary>Drops every point within <paramref name="tolerance"/> of the chord it sits under.</summary>
    public static IReadOnlyList<MapPoint> Simplify(IReadOnlyList<MapPoint> points, float tolerance)
    {
        if (points == null)
            throw new ArgumentNullException(nameof(points));
        if (points.Count < 3)
            return new List<MapPoint>(points);

        var keep = new bool[points.Count];
        keep[0] = keep[points.Count - 1] = true;
        var spans = new Stack<(int First, int Last)>();
        spans.Push((0, points.Count - 1));
        while (spans.Count > 0)
        {
            var (first, last) = spans.Pop();
            int farthest = -1;
            float farthestDistance = tolerance;
            for (int i = first + 1; i < last; i++)
            {
                float d = DistanceToChord(points[i], points[first], points[last]);
                if (d > farthestDistance)
                {
                    farthest = i;
                    farthestDistance = d;
                }
            }
            if (farthest < 0)
                continue;
            keep[farthest] = true;
            spans.Push((first, farthest));
            spans.Push((farthest, last));
        }

        var result = new List<MapPoint>();
        for (int i = 0; i < points.Count; i++)
        {
            if (keep[i])
                result.Add(points[i]);
        }
        return result;
    }

    /// <summary>
    /// Chaikin corner cutting. An open line keeps its endpoints and a line of fewer than three points
    /// is returned as is; a closed ring (first point not repeated) doubles its corners each pass.
    /// </summary>
    public static IReadOnlyList<MapPoint> Chaikin(IReadOnlyList<MapPoint> points, int iterations, bool closed)
    {
        if (points == null)
            throw new ArgumentNullException(nameof(points));
        var current = new List<MapPoint>(points);
        for (int pass = 0; pass < iterations; pass++)
        {
            if (current.Count < 3)
                return current;
            var next = new List<MapPoint>(current.Count * 2 + 2);
            int segments = closed ? current.Count : current.Count - 1;
            if (!closed)
                next.Add(current[0]);
            for (int i = 0; i < segments; i++)
            {
                MapPoint a = current[i];
                MapPoint b = current[(i + 1) % current.Count];
                next.Add(a * 0.75f + b * 0.25f);
                next.Add(a * 0.25f + b * 0.75f);
            }
            if (!closed)
                next.Add(current[current.Count - 1]);
            current = next;
        }
        return current;
    }

    /// <summary>Simplify, then smooth. A closed ring is split at its farthest point so both halves simplify.</summary>
    public static IReadOnlyList<MapPoint> Smooth(IReadOnlyList<MapPoint> points, bool closed, float tolerance, int iterations)
    {
        if (points == null)
            throw new ArgumentNullException(nameof(points));
        if (!closed || points.Count < 4)
            return Chaikin(Simplify(points, tolerance), iterations, closed);

        int far = 0;
        float farDistance = -1f;
        for (int i = 1; i < points.Count; i++)
        {
            float d = (points[i] - points[0]).LengthSquared;
            if (d > farDistance)
            {
                far = i;
                farDistance = d;
            }
        }

        var firstHalf = new List<MapPoint>();
        for (int i = 0; i <= far; i++)
            firstHalf.Add(points[i]);
        var secondHalf = new List<MapPoint>();
        for (int i = far; i < points.Count; i++)
            secondHalf.Add(points[i]);
        secondHalf.Add(points[0]);

        var ring = new List<MapPoint>(Simplify(firstHalf, tolerance));
        ring.RemoveAt(ring.Count - 1);
        ring.AddRange(Simplify(secondHalf, tolerance));
        ring.RemoveAt(ring.Count - 1);
        return Chaikin(ring, iterations, closed: true);
    }

    /// <summary>
    /// Evenly spaced points along a line, no more than <paramref name="spacing"/> apart, so a strip
    /// laid on them can follow the terrain between junctions. An open line keeps both ends; a closed
    /// ring does not repeat its start.
    /// </summary>
    public static IReadOnlyList<MapPoint> Resample(IReadOnlyList<MapPoint> points, float spacing, bool closed)
    {
        if (points == null)
            throw new ArgumentNullException(nameof(points));
        if (!(spacing > 0f))
            throw new ArgumentOutOfRangeException(nameof(spacing));
        if (points.Count < 2)
            return new List<MapPoint>(points);

        int segments = closed ? points.Count : points.Count - 1;
        var cumulative = new float[segments + 1];
        for (int i = 0; i < segments; i++)
            cumulative[i + 1] = cumulative[i] + (points[(i + 1) % points.Count] - points[i]).Length;
        float total = cumulative[segments];
        if (!(total > 0f))
            return new List<MapPoint> { points[0] };

        int steps = Math.Max(closed ? 3 : 1, (int)Math.Ceiling(total / spacing));
        float step = total / steps;
        var result = new List<MapPoint>(steps + 1);
        int segment = 0;
        int last = closed ? steps - 1 : steps;
        for (int k = 0; k <= last; k++)
        {
            float at = k == steps ? total : k * step;
            while (segment < segments - 1 && cumulative[segment + 1] < at)
                segment++;
            float length = cumulative[segment + 1] - cumulative[segment];
            float t = length > 0f ? (at - cumulative[segment]) / length : 0f;
            MapPoint a = points[segment], b = points[(segment + 1) % points.Count];
            result.Add(k == steps && !closed ? points[points.Count - 1] : a + (b - a) * t);
        }
        return result;
    }

    private static float DistanceToChord(MapPoint p, MapPoint a, MapPoint b)
    {
        MapPoint ab = b - a;
        float length = ab.Length;
        if (length < 1e-6f)
            return (p - a).Length;
        return Math.Abs(ab.X * (p.Y - a.Y) - ab.Y * (p.X - a.X)) / length;
    }
}
