using System;
using System.Collections.Generic;

namespace TAOM.Features.RealmBorders.Domain;

/// <summary>
/// Builds the band every border look is drawn with: the strip between two signed offsets from a line,
/// positive to the left of travel. Corners are mitred, with the mitre capped at
/// <c>miterLimit</c> times the offset so a hairpin cannot throw a vertex away; consecutive duplicate
/// points are dropped. <see cref="StripGeometry.V"/> is the distance along the line in world units,
/// which the ink's dash-dot pattern is measured in. The watercolour, the ink line, the gold cord and
/// the heraldic bands are all strips of this kind at different offsets.
/// </summary>
public static class StripBuilder
{
    public static StripGeometry Build(
        IReadOnlyList<MapPoint> line, bool closed, float nearOffset, float farOffset, float miterLimit = 2.5f)
    {
        if (line == null)
            throw new ArgumentNullException(nameof(line));
        if (!(miterLimit >= 1f))
            throw new ArgumentOutOfRangeException(nameof(miterLimit));

        var points = WithoutRepeats(line, closed);
        if (points.Count < 2)
            return StripGeometry.Empty;

        int count = points.Count;
        var near = new List<MapPoint>(count + 1);
        var far = new List<MapPoint>(count + 1);
        var v = new List<float>(count + 1);
        float along = 0f;
        for (int i = 0; i < count; i++)
        {
            if (i > 0)
                along += (points[i] - points[i - 1]).Length;
            MapPoint offsetDirection = MitredNormal(points, i, closed, miterLimit);
            near.Add(points[i] + offsetDirection * nearOffset);
            far.Add(points[i] + offsetDirection * farOffset);
            v.Add(along);
        }

        if (closed)
        {
            along += (points[0] - points[count - 1]).Length;
            near.Add(near[0]);
            far.Add(far[0]);
            v.Add(along);
        }
        return new StripGeometry(near, far, v, closed);
    }

    /// <summary>The left normal at a vertex, lengthened so the offset lines of both legs meet on it.</summary>
    private static MapPoint MitredNormal(List<MapPoint> points, int i, bool closed, float miterLimit)
    {
        int count = points.Count;
        bool hasPrevious = i > 0 || closed;
        bool hasNext = i < count - 1 || closed;
        MapPoint incoming = hasPrevious ? (points[i] - points[(i - 1 + count) % count]).Normalized() : default;
        MapPoint outgoing = hasNext ? (points[(i + 1) % count] - points[i]).Normalized() : default;
        if (!hasPrevious)
            return outgoing.Left;
        if (!hasNext)
            return incoming.Left;

        MapPoint bisector = incoming + outgoing;
        if (bisector.LengthSquared < 1e-8f)
            return incoming.Left; // a full reversal has no mitre: square it off
        MapPoint normal = bisector.Normalized().Left;
        float cos = normal.Dot(incoming.Left);
        return normal * (1f / Math.Max(cos, 1f / miterLimit));
    }

    private static List<MapPoint> WithoutRepeats(IReadOnlyList<MapPoint> line, bool closed)
    {
        var points = new List<MapPoint>(line.Count);
        foreach (MapPoint p in line)
        {
            if (points.Count == 0 || (p - points[points.Count - 1]).LengthSquared > 1e-10f)
                points.Add(p);
        }
        if (closed && points.Count > 1 && (points[0] - points[points.Count - 1]).LengthSquared <= 1e-10f)
            points.RemoveAt(points.Count - 1);
        return points;
    }
}
