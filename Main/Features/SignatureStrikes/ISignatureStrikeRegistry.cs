namespace TAOM.Features.SignatureStrikes;

/// <summary>
/// Answers "which signature does this agent carry" on three identity axes: hero StringId and
/// named hero set (the axes that survive a data change dropping the race attribute, and the one
/// that finds the Nine) and FaceGen race (the axis that finds a hero in a custom battle when the
/// character id is not listed). The hero axes win over race; within an axis the first signature
/// listed wins (#645).
///
/// Identity does not fold the master toggle: the roster registers every eligible agent at spawn,
/// and the toggle gates the EFFECT in <see cref="ISignatureStrikeService"/>, so a hero who spawned
/// while the feature was off starts striking the moment it is switched back on.
/// </summary>
public interface ISignatureStrikeRegistry
{
    /// <summary>The position in the validated config's <c>Signatures</c> list of the signature
    /// this identity carries, or null for none.</summary>
    int? ResolveSignatureIndex(string? heroStringId, int? raceId);

    /// <summary>The config id of that signature, for log lines; empty for an index out of range.</summary>
    string GetSignatureId(int signatureIndex);
}
