using System.Collections.Generic;

namespace TAOM.Features.RealmBorders.Domain;

/// <summary>
/// One stretch of edge between two provinces, from junction to junction. <see cref="Left"/> is the
/// lower province index and lies on the left of the direction <see cref="Points"/> run in, for the
/// whole chain. A closed chain rings an enclave and does not repeat its first point.
/// </summary>
public sealed class ProvinceBoundary
{
    public ProvinceBoundary(int left, int right, IReadOnlyList<LatticePoint> points, bool isClosed)
    {
        Left = left;
        Right = right;
        Points = points;
        IsClosed = isClosed;
    }

    public int Left { get; }

    public int Right { get; }

    public IReadOnlyList<LatticePoint> Points { get; }

    public bool IsClosed { get; }
}
