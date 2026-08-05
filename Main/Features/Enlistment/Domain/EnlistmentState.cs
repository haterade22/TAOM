namespace TAOM.Features.Enlistment.Domain;

/// <summary>
/// The persisted enlistment state machine. Party presence flags (IsActive/IsVisible) are
/// OUTPUTS asserted from this state by the attachment service — never inputs read back as
/// state (the donor mod's core defect).
///
/// Persistence notes:
///   - <see cref="EnlistedBattle"/> is transient — serialization coerces it to
///     <see cref="EnlistedAttached"/>; battle/encounter reality is re-derived from the
///     engine at load.
///   - <see cref="Discharging"/> is atomic and must never reach a save; the store logs an
///     anomaly and coerces if asked to serialize it.
///   - The ONLY legal path back to <see cref="NotEnlisted"/> from any enlisted-family state
///     runs through <see cref="Discharging"/> (see <see cref="EnlistmentTransitionTable"/>),
///     which is what makes the donor's commission-exit softlock unrepresentable.
/// </summary>
public enum EnlistmentState
{
    NotEnlisted = 0,
    PetitionPending = 1,
    EnlistedAttached = 2,
    EnlistedBattle = 3,

    /// <summary>
    /// RESERVED for the content phase (issue #375 Phase 3 field duties) — the reconciler,
    /// transition table, and normalizer already handle it, but nothing produces it until
    /// the duty system lands. Not a missed transition.
    /// </summary>
    EnlistedDetachedOnDuty = 4,
    EnlistedPlayerCaptive = 5,
    CommanderUnavailable = 6,
    Discharging = 7,
}
