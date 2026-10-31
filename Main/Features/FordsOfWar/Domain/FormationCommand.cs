namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>The decision for one lane formation. Emitted every tick; the engine layer applies it only when it changed.</summary>
internal sealed record FormationCommand(MatchSide Side, int Lane, LaneCommand Command);
