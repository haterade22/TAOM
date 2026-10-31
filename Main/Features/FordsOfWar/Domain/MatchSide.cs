namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>The two sides of a Fords of War match.</summary>
internal enum MatchSide
{
    West,
    East,
}

internal static class MatchSides
{
    public static MatchSide Opponent(MatchSide side) => side == MatchSide.West ? MatchSide.East : MatchSide.West;
}
