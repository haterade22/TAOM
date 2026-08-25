using TAOM.Adapters;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Features.Enlistment;

/// <summary>
/// The single authority on "is the main party correctly attached, and if not, what needs
/// doing". <see cref="Assess"/> is pure over snapshots; the execution methods are the only
/// path to party presence (via <see cref="IMobilePartyAttachmentAdapter"/>).
/// </summary>
public interface IServiceAttachmentService
{
    AttachmentAssessment Assess(
        EnlistmentState state, CommanderSnapshot commander, PlayerPresenceSnapshot player,
        bool onTownLeave);

    /// <summary>
    /// Raised with the settlement id after the follow transaction has fully landed, so a listener
    /// can offer the player shore leave while the column is actually stopped. Deliberately an
    /// event rather than a direct presenter call: this is a service, and popups are presentation.
    /// Mirrors <c>IServiceMaintenanceService.BattleJoinRequested</c>.
    /// </summary>
    event System.Action<string> ColumnEnteredSettlement;

    /// <summary>Pass the commander id or distToCommander reads -1 and the drift line prints '?'.</summary>
    PlayerPresenceSnapshot GetPresence(string commanderHeroId = null);

    bool EnsureParked(string commanderHeroId);

    bool SyncPosition(string commanderHeroId);

    bool RestorePresence();

    /// <summary>Pump-cadence position sync. Zero lookups on the steady path; see the adapter member.</summary>
    bool SyncPositionCached(string commanderHeroId, string expectedCommanderPartyId);

    /// <summary>Allocation-free presence read for the pump.</summary>
    PlayerPresenceFlags GetPresenceFlags();

    /// <summary>Drop the cached commander handle — discharge, session launch, game load.</summary>
    void InvalidateCommanderCache();

    /// <summary>Clear AttachedTo / non-led Army so the main party is a free agent again.</summary>
    bool ClearArmyAttachment();

    /// <summary>
    /// Follow the commander's column into a settlement, holding the player in the TAOM wait menu
    /// throughout — they are INSIDE, but never handed to vanilla town flow. One transaction;
    /// see the implementation for why it cannot be split.
    /// </summary>
    bool FollowCommanderIntoSettlement(string commanderHeroId, string settlementId);

    /// <summary>Leave a settlement the commander is not in and resume parked following.</summary>
    bool ExitSettlementForService(string commanderHeroId);
}
