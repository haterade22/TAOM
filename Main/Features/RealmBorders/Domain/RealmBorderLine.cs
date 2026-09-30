using System.Collections.Generic;

namespace TAOM.Features.RealmBorders.Domain;

/// <summary>
/// One drawn border between two realms. <see cref="LeftRealm"/> is the ordinally lower realm id and
/// lies on the left of the direction <see cref="Points"/> run in, which is what tells the renderer
/// which colour goes on which side. A closed line rings a realm's enclave.
/// </summary>
public sealed class RealmBorderLine
{
    public RealmBorderLine(string leftRealm, string rightRealm, IReadOnlyList<LatticePoint> points, bool isClosed)
    {
        LeftRealm = leftRealm;
        RightRealm = rightRealm;
        Points = points;
        IsClosed = isClosed;
    }

    public string LeftRealm { get; }

    public string RightRealm { get; }

    public IReadOnlyList<LatticePoint> Points { get; }

    public bool IsClosed { get; }
}
