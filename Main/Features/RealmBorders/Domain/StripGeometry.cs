using System.Collections.Generic;

namespace TAOM.Features.RealmBorders.Domain;

/// <summary>
/// A band along a border line: <see cref="Near"/> and <see cref="Far"/> are its two edges, vertex by
/// vertex, and <see cref="V"/> is the distance along the line in world units, which the ink's dash-dot
/// pattern is measured in. A closed band repeats its first vertex at the end, carrying the full length.
/// </summary>
public sealed class StripGeometry
{
    public static readonly StripGeometry Empty = new StripGeometry(new MapPoint[0], new MapPoint[0], new float[0], false);

    public StripGeometry(IReadOnlyList<MapPoint> near, IReadOnlyList<MapPoint> far, IReadOnlyList<float> v, bool isClosed)
    {
        Near = near;
        Far = far;
        V = v;
        IsClosed = isClosed;
    }

    public IReadOnlyList<MapPoint> Near { get; }

    public IReadOnlyList<MapPoint> Far { get; }

    public IReadOnlyList<float> V { get; }

    public bool IsClosed { get; }
}
