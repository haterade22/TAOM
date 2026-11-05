namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// Who wins when the time limit ends a match: the side with more towers standing, then the larger share of its hit points left, keep included.
/// A share and not a sum, because a faction's keep can start stronger. An exact tie goes to West, so a match always has a winner; it needs both
/// sides untouched for the whole match.
/// </summary>
internal static class TimeVictory
{
    internal const float WarningMinutes = 5f;

    internal static MatchSide Winner(TowerChain chain)
    {
        var west = chain.Standing(MatchSide.West);
        var east = chain.Standing(MatchSide.East);
        if (west.Towers != east.Towers)
            return west.Towers > east.Towers ? MatchSide.West : MatchSide.East;

        return east.HitPointShare > west.HitPointShare ? MatchSide.East : MatchSide.West;
    }
}
