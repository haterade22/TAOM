namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// The lane layout of a match, declared once: a stream is one side's units in one lane, numbered West first, so a stream's index
/// is <c>side * lanes + lane</c> and there are <c>Sides * lanes</c> of them. A match has at most <see cref="MaxLanes"/> lanes
/// (the four default formation classes).
/// </summary>
internal static class LaneStreams
{
    internal const int Sides = 2;
    internal const int MaxLanes = 4;

    internal static int Count(int lanes) => Sides * lanes;

    internal static int Index(MatchSide side, int lane, int lanes) => (int)side * lanes + lane;
}
