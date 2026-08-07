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

    /// <summary>
    /// Position sync that costs ZERO lookups on the steady path. Revalidates a cached party
    /// handle O(1) against <paramref name="expectedCommanderPartyId"/> — which must come from the
    /// SAME pass's tick snapshot, never from a value the caller cached across passes, so a
    /// commander party swap (party destroyed and re-created, army re-form) is visible within one
    /// pass instead of silently syncing to a dead party's frozen position.
    /// </summary>
    bool SyncPositionCached(string commanderHeroId, string expectedCommanderPartyId);

    /// <summary>Drop the cached commander-party handle. Must be called on discharge, session launch and game load.</summary>
    void InvalidateCommanderCache();

    /// <summary>Allocation-free presence read for a pump. <see cref="GetPresence"/> is the slow-path equivalent.</summary>
    PlayerPresenceFlags GetPresenceFlags();

    /// <summary>
    /// Clear <c>AttachedTo</c> (and <c>Army</c> when we are not its leader) so the main party is a
    /// free agent again.
    ///
    /// Enlistment NEVER sets <c>AttachedTo</c> — see docs/features/enlistment.md, "Why the player's
    /// party is NOT attached to the commander". Setting it without an <c>Army</c> is an unavoidable
    /// NRE in <c>DefaultEncounterGameMenuModel.GetGenericStateMenu()</c>, which <c>Campaign.Tick()</c>
    /// calls every tick on the open map. This method exists to CLEAR state a save or another system
    /// may have left behind, never to establish it.
    ///
    /// Leader carve-out: if the main party LEADS an army, clearing <c>Army</c> would disband it out
    /// from under its members. Leave a led army alone.
    /// </summary>
    bool ClearArmyAttachment();


    /// <summary>Presence + captivity snapshot for diagnostics and load-time rescue.</summary>
    PlayerPresenceSnapshot GetPresence();

    bool MoveIntoSettlement(string settlementId);

    bool LeaveSettlement();
}
