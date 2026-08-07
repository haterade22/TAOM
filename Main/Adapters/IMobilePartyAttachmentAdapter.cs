namespace TAOM.Adapters;

/// <summary>
/// The ONLY writer of main-party presence (IsActive/IsVisible/position/camera) in the
/// enlistment feature. Park/restore are asserted from the persisted state machine —
/// presence flags are outputs, never inputs. Deliberately never touches
/// <c>MobileParty.Army</c>: parking is position-sync + hidden/inactive, and battle join
/// works through the encounter layer without army membership.
/// </summary>
public interface IMobilePartyAttachmentAdapter
{
    /// <summary>Hide + deactivate the main party at the commander's position, camera on the commander.</summary>
    bool ParkNear(string commanderHeroId);

    /// <summary>
    /// Activate + show the main party and return the camera to it. Idempotent; safe to
    /// call in any state. This is the discharge pipeline's unconditional first-restore step.
    /// </summary>
    bool RestorePresence();

    /// <summary>Teleport the parked main party to the commander's position. False when the commander party is gone.</summary>
    bool SyncPositionTo(string commanderHeroId);

    /// <summary>Distance from the main party to the commander's party, or -1 when unresolvable. Diagnostics only.</summary>
    float GetDistanceToCommander(string commanderHeroId);

    /// <summary>Presence + captivity snapshot for diagnostics and load-time rescue.</summary>
    PlayerPresenceSnapshot GetPresence();

    bool MoveIntoSettlement(string settlementId);

    bool LeaveSettlement();
}
