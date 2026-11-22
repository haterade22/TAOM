namespace TAOM.Features.CareerSystem;

/// <summary>
/// Which career tiers count in the passive cache (docs/features/coop-interop.md, "Career sync"). A tier opens at a
/// level or through a career quest, and a quest runs on the player's own machine. The co-op server cannot see that
/// unlock, so there a tier counts only from the hero's level; everywhere else every tier counts.
/// </summary>
public interface ICareerTierTrust
{
    /// <summary>True when a choice in <paramref name="tier"/> adds its passives for this hero. Tier 0 is a choice with no group.</summary>
    bool Counts(string heroId, int tier);
}
