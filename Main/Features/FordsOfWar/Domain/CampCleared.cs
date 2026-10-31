namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// A neutral camp lost its last unit. Side is the side whose unit made the last kill, Slot the commander slot that gets
/// the Reward. An unknown killer, or a map without lanes, gives Slot -1 and Reward 0.
/// </summary>
internal sealed record CampCleared(string CampId, MatchSide? Side, int Slot, int Reward);
