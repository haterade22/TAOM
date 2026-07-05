namespace TAOM.Features.ArmyTargeting.Diagnostics;

/// <summary>
/// Why an AI army could not resolve a gathering fortification in vanilla
/// <c>Army.FindBestGatheringSettlementAndMoveTheLeader</c> (the dead end guarded by
/// Patch49). Inferred purely from <see cref="SiegeGatheringFailureInfo"/> counts — the
/// crash at Army.cs:726 implies both the primary selection loop AND
/// <c>FindNearestFortificationToMobileParty</c> returned null.
/// </summary>
public enum SiegeGatheringFailureReason
{
    /// <summary>Army leader's clan is not in a kingdom (<c>Kingdom == null</c>) — Army.cs:659.</summary>
    KingdomNull,

    /// <summary>The kingdom owns no fortifications at all.</summary>
    NoFortifications,

    /// <summary>Every kingdom fortification is currently under siege.</summary>
    AllFortificationsUnderSiege,

    /// <summary>
    /// Fortifications exist and not all are under siege, yet none was navigable / in range
    /// from the leader party — the interesting map / navmesh case worth fixing.
    /// </summary>
    NoReachableFortification,

    /// <summary>Counts were unavailable (defensive fallback).</summary>
    Unknown
}
