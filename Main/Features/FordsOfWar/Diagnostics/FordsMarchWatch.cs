using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.FordsOfWar.Diagnostics;

/// <summary>
/// The check behind check 9's <c>bypass</c> part: a stream that marches while most of its own units stand past the next standing enemy
/// tower has walked by a tower it should have attacked. The measure is the units' positions at the 1 Hz probe, not the rules' lead,
/// so a fault in the lead cannot hide itself. Pure, so the arithmetic is pinned by a test.
/// </summary>
internal static class FordsMarchWatch
{
    /// <summary>How far past a tower's arc a unit must stand to count as past it: the tower's reach is well inside this.</summary>
    internal const float PastMetres = 10f;

    /// <summary>The looks in a row on which most units stand past the tower before the part fails: one look can be a wave passing.</summary>
    internal const int ConsecutiveLooks = 2;

    /// <summary>
    /// True when a unit at <paramref name="unitArc"/> stands at least <see cref="PastMetres"/> beyond the tower at
    /// <paramref name="towerArc"/> in its side's direction of travel (West walks toward higher arcs, East toward lower ones).
    /// </summary>
    internal static bool IsPast(MatchSide side, float unitArc, float towerArc) =>
        side == MatchSide.West ? unitArc >= towerArc + PastMetres : unitArc <= towerArc - PastMetres;

    /// <summary>True when more than half of a stream's live units are past the tower.</summary>
    internal static bool MostPast(int past, int live) => live > 0 && past * 2 > live;
}
