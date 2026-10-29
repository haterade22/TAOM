using TAOM.Features.StagedSiege.Domain;

namespace TAOM.Features.StagedSiege;

/// <summary>
/// The seam Creature Siege Role reads to learn whether the staged siege is steering a side's formations (docs/features/staged-siege.md,
/// "Sharing the field"). An interface because it crosses two features and the role's tests fake it. Reads only the immutable
/// <see cref="Domain.StageSnapshot"/>, so any thread may call it.
/// </summary>
public interface IStagedSiegeFieldReader
{
    /// <summary>
    /// True while a staged siege is armed for <paramref name="mission"/>, not finished, and the current stage gives
    /// <paramref name="side"/> a tactic other than vanilla's.
    /// </summary>
    bool OwnsField(object mission, StagedSide side);
}
