namespace TAOM.Features.SignatureStrikes;

/// <summary>
/// Answers "does this agent have signature strikes" on two identity axes, OR'd: hero StringId
/// (the axis that survives a data change dropping the race attribute) and FaceGen race (the axis
/// that finds the hero in a custom battle, where the agent carries no HeroObject).
///
/// Identity does not fold the master toggle: the roster registers every eligible agent at spawn,
/// and the toggle gates the EFFECT in <see cref="ISignatureStrikeService"/>, so a hero who spawned
/// while the feature was off starts striking the moment it is switched back on.
/// </summary>
public interface ISignatureStrikeRegistry
{
    bool IsSignatureAgent(string? heroStringId, int? raceId);
}
