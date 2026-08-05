namespace TAOM.Features.Enlistment.Domain;

/// <summary>
/// Why a service term ended. Consumed by the discharge pipeline to pick consequences
/// (rewards, penalties, messages); every reason runs the same fixed-order pipeline that
/// unconditionally restores party presence.
///
/// PRODUCER STATUS (core phase): PlayerRequest, CommanderDead,
/// CommanderUnavailableGraceExpired and HeirSuccessionOrPossessionMismatch are live.
/// Commission / Desertion / ContractNotRenewed are RESERVED for the content phase
/// (issue #375 Phase 3: contract-expiry check + the honorable-vs-desertion split of the
/// leave flow); SaveNormalization is reserved for load paths that can safely run the
/// world-touching pipeline — see the note on <see cref="SaveNormalization"/>.
/// </summary>
public enum DischargeReason
{
    /// <summary>Contract served out or player asked to leave at a legal point.</summary>
    PlayerRequest = 0,

    /// <summary>Commission retirement — honorable exit with distinction rewards. (Content phase.)</summary>
    Commission = 1,

    /// <summary>Walked out mid-contract — relation/renown penalties, deferred wages forfeit. (Content phase.)</summary>
    Desertion = 2,

    CommanderDead = 3,

    /// <summary>Commander stayed party-less (captured/disbanded) past the grace window.</summary>
    CommanderUnavailableGraceExpired = 4,

    /// <summary>(Content phase — requires the contract-expiry check.)</summary>
    ContractNotRenewed = 5,

    /// <summary>EnlistedHeroId no longer matches MainHero (co-op join, heir succession). Quiet, penalty-free.</summary>
    HeirSuccessionOrPossessionMismatch = 6,

    /// <summary>
    /// Load-time normalization could not faithfully restore service. Quiet, penalty-free.
    /// NOTE: EnlistmentStore.Deserialize failures deliberately do NOT route here — SyncData
    /// runs before the world is valid, so the store resets record-only and the load
    /// normalizer's ownerless-parked rescue handles the world side. This reason exists for
    /// normalizer-driven discharges once one needs a distinct label from the identity guard.
    /// </summary>
    SaveNormalization = 7,
}
