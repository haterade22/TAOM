namespace TAOM.Features.EncounterLines;

/// <summary>
/// Where Bandit Voices (Patch122) asks which bucket the player is in for a bandit clan. Implemented by
/// <see cref="EncounterLinesService"/>; Bandit Voices receives null when the Encounter Lines module is not registered.
/// </summary>
public interface IEncounterBucketSource
{
    /// <summary>"kin", "side" or "foe" for a clan <c>encounter_groups.json</c> lists; null for any other clan, with the feature off, or with no player.</summary>
    string? BanditBucket(string? clanId);
}
