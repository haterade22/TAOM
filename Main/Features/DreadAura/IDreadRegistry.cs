namespace TAOM.Features.DreadAura;

/// <summary>
/// Answers the two race/hero lookups the dread aura needs: who projects it, and who resists it.
/// Both share one lazily-built race-name to race-id map (the FaceGen registry is not populated when
/// the singleton is constructed), which is why they live on one interface rather than two.
///
/// Neither method folds the master toggle. Identity is not a decision the toggle should change:
/// gating registration on it would let a wraith that spawned during an off-window stay permanently
/// unregistered after the player switches the feature back on. The toggle gates the DRAIN, in
/// <see cref="IDreadAuraService"/> and in the mission tick.
/// </summary>
public interface IDreadRegistry
{
    /// <summary>
    /// True if this agent projects dread.
    ///
    /// Two identity axes, OR'd. Hero StringId catches the Nine (who are NOT identifiable by race
    /// in TAOM's data) and Sauron; race catches Sauron and anything a future config row adds.
    /// </summary>
    bool IsDreadSource(string heroStringId, int? raceId);

    /// <summary>
    /// The target's resistance multiplier on incoming dread, in [0, 1]. 1.0 when the race is
    /// unknown, invalid, or absent from the table.
    /// </summary>
    float ResolveResist(int? targetRaceId);
}
