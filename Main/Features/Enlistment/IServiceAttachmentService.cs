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
}
