namespace TAOM.Features.SiegePropDiagnostics.Models;

/// <summary>
/// Why a resupply prop is (or is not) usable by the player. Ordered roughly by the differential
/// built from the engine read: scene-data faults first, then prop state, then the player-side
/// probe. Every one of these fails SILENTLY in-game — the engine logs nothing for any of them,
/// which is the whole reason this diagnostic exists.
/// </summary>
public enum SiegePropDiagnosis
{
    /// <summary>The player's standing-point probe succeeded — this prop is usable right now.</summary>
    Healthy = 0,

    /// <summary>
    /// The machine collected no standing points at all. Scene fault: the entity carries the
    /// script but none of the prefab's child points came with it.
    /// </summary>
    NoStandingPoints,

    /// <summary>
    /// A rock pile with no <c>ammopickup</c>-tagged point. <c>StonePile.OnInit</c> only calls
    /// <c>InitGivenWeapon</c> on tagged points, so an untagged pile can never hand out a boulder.
    /// </summary>
    NoAmmoPickupPoints,

    /// <summary>
    /// <c>GivenItemID</c> does not resolve to an item. <c>_givenItem</c> is null, the point gets
    /// <c>InitGivenWeapon(null)</c>, and <c>StandingPointWithWeaponRequirement.IsDisabledForAgent</c>
    /// falls through to <c>return true</c> — permanently disabled for player AND AI, silently.
    /// </summary>
    ItemIdUnresolved,

    /// <summary>The whole machine is disabled (<c>MissionObject.IsDisabled</c>), e.g. destroyed or sunk.</summary>
    MachineDisabled,

    /// <summary>A rock pile whose boulders are gone. <c>CheckAmmo</c> has deactivated every pickup point.</summary>
    AmmoExhausted,

    /// <summary>Every pickup point is deactivated but ammo remains — deactivated by something other than the ammo counter.</summary>
    AllPointsDeactivated,

    /// <summary>The player is mounted. <c>UsableMissionObject.IsDisabledForAgent</c> rejects every usable object while <c>MountAgent != null</c>.</summary>
    PlayerMounted,

    /// <summary>
    /// Points exist and are active, but all report disabled for the player. For a barrel this is
    /// usually vanilla and correct: no Arrow/Bolt slot below max means nothing to refill.
    /// </summary>
    AllPointsDisabledForPlayer,

    /// <summary>
    /// The point's resolved ground height differs from the player's by 1.5m or more, which fails
    /// the reachability test in <c>GetValidVacantReachableStandingPointForAgent</c>. Usually missing
    /// navmesh under the prop.
    /// </summary>
    GroundHeightMismatch,

    /// <summary>The player is simply too far away. Player interaction distance to a standing point is 2m.</summary>
    PlayerOutOfRange,

    /// <summary>Every point is occupied by another agent.</summary>
    AllPointsOccupied,

    /// <summary>The probe failed and no known cause matched. Dump the raw snapshot and widen the differential.</summary>
    UnknownProbeFailure,
}
