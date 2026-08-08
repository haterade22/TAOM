using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Features.Enlistment;

public class ServiceAttachmentService : IServiceAttachmentService
{
    private readonly IMobilePartyAttachmentAdapter _attachment;
    private readonly IGameMenuAdapter _gameMenu;
    private readonly IModLogger _logger;

    public ServiceAttachmentService(
        IMobilePartyAttachmentAdapter attachment,
        IGameMenuAdapter gameMenu,
        IModLogger logger)
    {
        _attachment = attachment;
        _gameMenu = gameMenu;
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

        var playerSettlement = player.SettlementId;
        var commanderSettlement = commander.PartyIsInSettlement ? commander.SettlementId : null;
        var playerIsSomewhere = !string.IsNullOrEmpty(playerSettlement);
        var sameSettlement = playerIsSomewhere
            && !string.IsNullOrEmpty(commanderSettlement)
            && string.Equals(playerSettlement, commanderSettlement, System.StringComparison.Ordinal);

        // ORDER IS LOAD-BEARING: above the battle branch, deliberately. Joining a map event while
        // CurrentSettlement still points at some OTHER settlement puts the party in two places at
        // once, and MapEvent.AddInvolvedPartyInternal rewrites a siege ASSAULT to SiegeOutside off
        // that field for a joining defender — turning an assault on the walls into a field fight
        // for every participant. Being inside the settlement the commander is defending is the
        // CORRECT state and is exempt; being inside a different one is not.
        if (playerIsSomewhere && !sameSettlement)
            return new AttachmentAssessment(AttachmentStatus.SettlementExitRequired);

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

        // Already inside with him: correct, and NOT parked — the party is deliberately active and
        // visible, pinned to the gate. Returning AttachRequired here would re-park the player back
        // outside every hour, which is the same "standing at the gate" bug with extra steps.
        if (sameSettlement)
            return new AttachmentAssessment(AttachmentStatus.Attached);

        if (!string.IsNullOrEmpty(commanderSettlement))
            return new AttachmentAssessment(AttachmentStatus.SettlementFollowRequired);

        return player.LooksParked
            ? new AttachmentAssessment(AttachmentStatus.Attached)
            : new AttachmentAssessment(AttachmentStatus.AttachRequired);
    }

    public PlayerPresenceSnapshot GetPresence(string commanderHeroId = null) => _attachment.GetPresence(commanderHeroId);

    public bool EnsureParked(string commanderHeroId) => _attachment.ParkNear(commanderHeroId);

    public bool SyncPosition(string commanderHeroId) => _attachment.SyncPositionTo(commanderHeroId);

    public bool RestorePresence() => _attachment.RestorePresence();

    public bool SyncPositionCached(string commanderHeroId, string expectedCommanderPartyId) =>
        _attachment.SyncPositionCached(commanderHeroId, expectedCommanderPartyId);

    public PlayerPresenceFlags GetPresenceFlags() => _attachment.GetPresenceFlags();

    public void InvalidateCommanderCache() => _attachment.InvalidateCommanderCache();

    public bool ClearArmyAttachment() => _attachment.ClearArmyAttachment();

    /// <summary>
    /// ONE transaction, never separable: restore presence, move in, then assert the wait menu.
    /// A settlement placement WITHOUT the wait menu is the donor's crash state — the player
    /// reaches a vanilla settlement menu with no <c>PlayerEncounter</c>, and
    /// <c>game_menu_settlement_wait_on_init</c> opens by dereferencing
    /// <c>PlayerEncounter.EncounterSettlement</c>. Any step failing rolls the move back out.
    /// </summary>
    public bool FollowCommanderIntoSettlement(string commanderHeroId, string settlementId)
    {
        if (string.IsNullOrEmpty(settlementId))
            return false;

        // The engine skips inactive parties in settlement placement, so presence comes first —
        // the same ordering the battle path needs, for the same reason.
        if (!_attachment.RestorePresence())
        {
            _logger?.LogError($"[EnlistDiag] FOLLOW into '{settlementId}' aborted — could not restore presence first");
            return false;
        }

        if (!_attachment.MoveIntoSettlement(settlementId))
        {
            _logger?.LogError($"[EnlistDiag] FOLLOW into '{settlementId}' failed — re-parking outside instead");
            _attachment.ParkNear(commanderHeroId);
            return false;
        }

        if (!_gameMenu.EnsureMenuOpen(EnlistmentMenuService.ServiceWaitMenuId))
        {
            // MANDATORY rollback. Inside a settlement with no menu of ours is exactly the state
            // that crashes on the next vanilla settlement-menu init.
            _logger?.LogError($"[EnlistDiag] FOLLOW into '{settlementId}' could not open the service menu — leaving rather than sitting in a settlement with no menu");
            _attachment.LeaveSettlement();
            _attachment.ParkNear(commanderHeroId);
            return false;
        }

        _logger?.LogInfo($"[EnlistDiag] FOLLOW: entered '{settlementId}' with the commander's column");
        return true;
    }

    /// <summary>
    /// Leave a settlement the commander is not in, and go back to following. Runs before any
    /// battle join — see <see cref="AttachmentStatus.SettlementExitRequired"/>.
    /// </summary>
    public bool ExitSettlementForService(string commanderHeroId)
    {
        if (!_attachment.LeaveSettlement())
        {
            _logger?.LogError("[EnlistDiag] EXIT failed — the player is stuck inside a settlement the commander has left");
            return false;
        }

        _logger?.LogInfo("[EnlistDiag] EXIT: left the settlement to rejoin the column");
        return _attachment.ParkNear(commanderHeroId);
    }

    /// <summary>
    /// Leave a settlement and stay VISIBLE — the detached-duty exit.
    ///
    /// Distinct from <see cref="ExitSettlementForService"/> on purpose. That one ends in a park,
    /// which is correct while following the column and catastrophic on a duty: parking hides and
    /// deactivates the party, and NOTHING un-hides it while the state is EnlistedDetachedOnDuty
    /// (Assess returns Blocked/NotInAttachableState for that state, so neither the reconciler nor
    /// the pump ever restores presence). The player would be invisible and immobile for the whole
    /// duty deadline — four to six days — then fail it.
    /// </summary>
    public bool ExitSettlementForDuty()
    {
        if (!_attachment.LeaveSettlement())
        {
            _logger?.LogError("[EnlistDiag] duty EXIT failed — the player is starting a duty while stuck inside a settlement");
            return false;
        }

        // Presence LAST and unconditional: a duty is performed out in the world.
        return _attachment.RestorePresence();
    }
}
