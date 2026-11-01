using System;
using System.Collections.Generic;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// One lane's polyline with its per-segment numbers worked out once (start, direction, length, squared length, arc at the
/// start), because a lane's path never changes after the layout fit and every tick asks it about every unit. The arc position
/// follows <see cref="LaneGeometry.ArcPosition"/> step for step, so the two give the same float.
/// </summary>
internal sealed class LanePath
{
    private const float Epsilon = 1e-3f;

    /// <summary>A lane's front is its k-th most advanced unit, where k is a tenth of the lane's units but never fewer than this.</summary>
    private const int MinFrontRank = 3;

    private readonly IReadOnlyList<Vec3f> _points;
    private readonly float[] _startX;
    private readonly float[] _startY;
    private readonly float[] _dx;
    private readonly float[] _dy;
    private readonly float[] _length;
    private readonly float[] _lengthSquared;
    private readonly float[] _arcAtStart;
    private readonly float[] _arcAtVertex;

    public LanePath(IReadOnlyList<Vec3f> points)
    {
        _points = points;
        var segments = Math.Max(0, points.Count - 1);
        _startX = new float[segments];
        _startY = new float[segments];
        _dx = new float[segments];
        _dy = new float[segments];
        _length = new float[segments];
        _lengthSquared = new float[segments];
        _arcAtStart = new float[segments];
        _arcAtVertex = new float[points.Count];

        var walked = 0f;
        for (var i = 0; i < segments; i++)
        {
            var a = points[i];
            var b = points[i + 1];
            _startX[i] = a.X;
            _startY[i] = a.Y;
            _dx[i] = b.X - a.X;
            _dy[i] = b.Y - a.Y;
            _length[i] = (float)Math.Sqrt(_dx[i] * _dx[i] + _dy[i] * _dy[i]);
            _lengthSquared[i] = _length[i] * _length[i];
            _arcAtStart[i] = walked;
            walked += _length[i];
            _arcAtVertex[i + 1] = walked;
        }

        Length = walked;
    }

    /// <summary>The lane's vertices, West base first.</summary>
    public IReadOnlyList<Vec3f> Points => _points;

    /// <summary>The lane's length in the ground plane; 0 for a path under two points.</summary>
    public float Length { get; }

    /// <summary>Arc length from the West end to the closest point of the lane; the first segment wins a tie.</summary>
    public float ArcPosition(Vec3f point)
    {
        var bestDistance = float.MaxValue;
        var bestArc = 0f;
        for (var i = 0; i < _length.Length; i++)
        {
            var t = _length[i] <= 0f
                ? 0f
                : ((point.X - _startX[i]) * _dx[i] + (point.Y - _startY[i]) * _dy[i]) / _lengthSquared[i];
            t = Math.Max(0f, Math.Min(1f, t));
            var px = _startX[i] + _dx[i] * t;
            var py = _startY[i] + _dy[i] * t;
            var d = (float)Math.Sqrt((point.X - px) * (point.X - px) + (point.Y - py) * (point.Y - py));
            if (d < bestDistance - Epsilon * 0.01f)
            {
                bestDistance = d;
                bestArc = _arcAtStart[i] + _length[i] * t;
            }
        }

        return bestArc;
    }

    /// <summary>
    /// The front of a formation. The front line is the advance of the k-th most advanced unit (k a tenth of the units, at least
    /// three, at most all of them), so a few runaways cannot drag it. The lead group is the units within
    /// <paramref name="band"/> metres of lane either side of that line (highest arc is the most advanced for West, lowest for
    /// East). Returns their mean position, their mean arc and how many they are; null for a path under two points or no
    /// readable position (a NaN coordinate is skipped).
    /// </summary>
    public (Vec3f Position, float Arc, int Count)? LeadGroup(MatchSide side, IReadOnlyList<Vec3f> positions, float band)
    {
        if (_points.Count < 2)
            return null;

        var arcs = new float[positions.Count];
        var advances = new float[positions.Count];
        var readable = 0;
        for (var i = 0; i < positions.Count; i++)
        {
            var p = positions[i];
            if (float.IsNaN(p.X) || float.IsNaN(p.Y))
            {
                arcs[i] = float.NaN;
                continue;
            }

            arcs[i] = ArcPosition(p);
            advances[readable++] = Advance(side, arcs[i]);
        }

        if (readable == 0)
            return null;

        var front = FrontLine(advances, readable);
        float x = 0f, y = 0f, z = 0f, arc = 0f;
        var count = 0;
        for (var i = 0; i < positions.Count; i++)
        {
            if (float.IsNaN(arcs[i]) || Math.Abs(Advance(side, arcs[i]) - front) > band)
                continue;

            x += positions[i].X;
            y += positions[i].Y;
            z += positions[i].Z;
            arc += arcs[i];
            count++;
        }

        return count == 0 ? null : (new Vec3f(x / count, y / count, z / count), arc / count, count);
    }

    /// <summary>The advance of the k-th most advanced of the first <paramref name="readable"/> entries; sorts a copy.</summary>
    private static float FrontLine(float[] advances, int readable)
    {
        var sorted = new float[readable];
        Array.Copy(advances, sorted, readable);
        Array.Sort(sorted);
        var rank = Math.Min(readable, Math.Max(MinFrontRank, (readable + 9) / 10));
        return sorted[readable - rank];
    }

    /// <summary>How far along the lane a unit is in its own side's direction of travel.</summary>
    private float Advance(MatchSide side, float arc) => side == MatchSide.West ? arc : Length - arc;

    /// <summary>
    /// The index of the waypoint a formation of <paramref name="side"/> should walk to when its front stands at
    /// <paramref name="leadArc"/> metres along the lane: the first waypoint ahead of it (toward the enemy base) that it has
    /// not yet come within <paramref name="reachRadius"/> metres of lane of. Clamps at the enemy end; -1 for an empty path.
    /// The test is on the arc, not the 2D distance, so a settled formation whose Move target is its front rank still advances.
    /// </summary>
    public int WaypointIndex(MatchSide side, float leadArc, float reachRadius)
    {
        var count = _points.Count;
        if (count == 0)
            return -1;

        var last = count - 1;
        if (side == MatchSide.West)
        {
            for (var i = 0; i < count; i++)
            {
                if (_arcAtVertex[i] - reachRadius > leadArc)
                    return i;
            }

            return last;
        }

        for (var i = last; i >= 0; i--)
        {
            if (_arcAtVertex[i] + reachRadius < leadArc)
                return i;
        }

        return 0;
    }

    /// <summary>
    /// Where <paramref name="holder"/> holds against <paramref name="tower"/>: <paramref name="distance"/> metres of lane short of
    /// it, but never past the lane midpoint, and at the midpoint when the target is a keep. The result is the path vertex nearest
    /// that arc on the holder's own side of it, so it is always a point the fitter kept (and kept walkable).
    /// </summary>
    public Vec3f HoldPoint(Vec3f tower, MatchSide holder, float distance, bool towerIsKeep)
    {
        if (_points.Count == 0)
            return default;

        var half = Length / 2f;
        var arc = half;
        if (!towerIsKeep)
        {
            var towerArc = ArcPosition(tower);
            arc = holder == MatchSide.West ? Math.Min(towerArc - distance, half) : Math.Max(towerArc + distance, half);
        }

        return _points[VertexIndexOnSide(holder, arc)];
    }

    /// <summary>The vertex nearest <paramref name="arc"/> that does not lie past it toward the enemy: West takes the highest arc at or below it, East the lowest at or above it.</summary>
    private int VertexIndexOnSide(MatchSide side, float arc)
    {
        if (side == MatchSide.West)
        {
            for (var i = _points.Count - 1; i > 0; i--)
            {
                if (_arcAtVertex[i] <= arc + Epsilon)
                    return i;
            }

            return 0;
        }

        for (var i = 0; i < _points.Count - 1; i++)
        {
            if (_arcAtVertex[i] >= arc - Epsilon)
                return i;
        }

        return _points.Count - 1;
    }
}
