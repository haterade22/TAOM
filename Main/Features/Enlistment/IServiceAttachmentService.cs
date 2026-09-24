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

    /// <summary>
    /// Raised when <see cref="ExitSettlementForService"/> has walked the player out of the stop
    /// (even if the re-park then failed): the stop is over. The arrival offer's settlement latch is
    /// cleared on it. Not the only stop end: a shore-leave pass suspends the exit sweep, so the
    /// commander's settlement-left edge (<c>EnlistmentMaintenanceBehavior</c>) clears it too.
    /// </summary>
    event System.Action ColumnLeftSettlement;

    /// <summary>
    /// True while the player is inside a settlement we placed them in less than
    /// <see cref="ServiceAttachmentService.SettlementDwellHours"/> campaign hours ago.
    ///
    /// Exists to stop the strobe. Measured 2026-08-25: an AI lord dips into a town for under an
    /// hour of campaign time, and following him in and straight back out produced ten transitions
    /// across three towns in three real minutes, median 2.5 seconds inside, twice entering and
    /// leaving within the SAME second. Nothing is usable in that window.
    /// </summary>
    bool IsWithinSettlementDwell(double nowHours);

    /// <summary>Record when a placement happened, so the dwell above can be measured from it.</summary>
    void StampSettlementEntry(double nowHours);

    /// <summary>
    /// Forget the dwell anchor and the adapter's cached commander party. Session reset only (a
    /// load, a new campaign or game end), never per tick: the anchor is an absolute campaign hour,
    /// and one left in the future reads as "inside the dwell" until the new clock passes it plus
    /// the 6-hour dwell. The cached party is matched by StringId, which a later campaign can reissue.
    /// </summary>
    void ResetForNewSession();

    /// <summary>Pass the commander id or distToCommander reads -1 and the drift line prints '?'.</summary>
    PlayerPresenceSnapshot GetPresence(string commanderHeroId = null);

    bool EnsureParked(string commanderHeroId);

    bool SyncPosition(string commanderHeroId);

    bool RestorePresence();

    /// <summary>Pump-cadence position sync. Zero lookups on the steady path; see the adapter member.</summary>
    bool SyncPositionCached(string commanderHeroId, string expectedCommanderPartyId);

    /// <summary>Allocation-free presence read for the pump.</summary>
    PlayerPresenceFlags GetPresenceFlags();

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
