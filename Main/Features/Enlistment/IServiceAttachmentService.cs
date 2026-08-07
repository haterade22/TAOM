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
    AttachmentAssessment Assess(EnlistmentState state, CommanderSnapshot commander, PlayerPresenceSnapshot player);

    PlayerPresenceSnapshot GetPresence();

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
}
