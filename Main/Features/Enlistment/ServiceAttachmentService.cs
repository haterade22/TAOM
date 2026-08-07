using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Features.Enlistment;

public class ServiceAttachmentService : IServiceAttachmentService
{
    private readonly IMobilePartyAttachmentAdapter _attachment;
    private readonly IModLogger _logger;

    public ServiceAttachmentService(IMobilePartyAttachmentAdapter attachment, IModLogger logger)
    {
        _attachment = attachment;
        _logger = logger;
    }

    public AttachmentAssessment Assess(EnlistmentState state, CommanderSnapshot commander, PlayerPresenceSnapshot player)
    {
        if (state != EnlistmentState.EnlistedAttached && state != EnlistmentState.EnlistedBattle)
            return new AttachmentAssessment(AttachmentStatus.Blocked, AttachmentBlockReason.NotInAttachableState);

        if (player.IsCaptive)
            return new AttachmentAssessment(AttachmentStatus.Blocked, AttachmentBlockReason.PlayerCaptive);

        // IsPrisoner is checked explicitly even though a captured hero's PartyBelongedTo
        // goes null in practice — the engine correlation is not guaranteed by contract,
        // and this keeps the fitness criteria identical to IsCommanderFit/ReconcileGrace.
        if (commander == null || !commander.Exists || !commander.IsAlive || commander.IsPrisoner
            || !commander.HasParty || !commander.PartyIsActive)
        {
            return new AttachmentAssessment(AttachmentStatus.Blocked, AttachmentBlockReason.CommanderPartyMissing);
        }

        if (commander.PartyIsInMapEvent)
        {
            // Same-event verification is the battle service's job; here "player is in some
            // map event" while the commander fights means there is nothing to attach.
            return player.IsInMapEvent
                ? new AttachmentAssessment(AttachmentStatus.Attached)
                : new AttachmentAssessment(AttachmentStatus.BattleJoinRequired);
        }

        if (player.IsInMapEvent)
            return new AttachmentAssessment(AttachmentStatus.Blocked, AttachmentBlockReason.PlayerInForeignMapEvent);

        return player.LooksParked
            ? new AttachmentAssessment(AttachmentStatus.Attached)
            : new AttachmentAssessment(AttachmentStatus.AttachRequired);
    }

    public PlayerPresenceSnapshot GetPresence() => _attachment.GetPresence();

    public bool EnsureParked(string commanderHeroId) => _attachment.ParkNear(commanderHeroId);

    public bool SyncPosition(string commanderHeroId) => _attachment.SyncPositionTo(commanderHeroId);

    public bool RestorePresence() => _attachment.RestorePresence();

    public bool SyncPositionCached(string commanderHeroId, string expectedCommanderPartyId) =>
        _attachment.SyncPositionCached(commanderHeroId, expectedCommanderPartyId);

    public PlayerPresenceFlags GetPresenceFlags() => _attachment.GetPresenceFlags();

    public void InvalidateCommanderCache() => _attachment.InvalidateCommanderCache();

    public bool ClearArmyAttachment() => _attachment.ClearArmyAttachment();
}
