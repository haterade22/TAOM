namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// The decision for one lane formation. Emitted every tick; the engine layer applies it only when it changed. Arrangement is the
/// faction tactic's arrangement for this decision, null when the faction names none.
/// </summary>
internal sealed record FormationCommand(MatchSide Side, int Lane, LaneCommand Command, LaneArrangement? Arrangement = null);
