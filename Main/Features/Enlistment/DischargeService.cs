using System;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Features.Enlistment;

public class DischargeService : IDischargeService
{
    private readonly IEnlistmentStore _store;
    private readonly IEnlistmentStateMachine _machine;
    private readonly IMobilePartyAttachmentAdapter _attachment;
    private readonly IEncounterAdapter _encounter;
    private readonly IEncounterOwnershipPolicy _ownership;
    private readonly ICommanderLordAdapter _commander;
    private readonly IGameMenuAdapter _gameMenu;
    private readonly IModLogger _logger;

    public DischargeService(
        IEnlistmentStore store,
        IEnlistmentStateMachine machine,
        IMobilePartyAttachmentAdapter attachment,
        IEncounterAdapter encounter,
        IEncounterOwnershipPolicy ownership,
        ICommanderLordAdapter commander,
        IGameMenuAdapter gameMenu,
        IModLogger logger)
    {
        _store = store;
        _machine = machine;
        _attachment = attachment;
        _encounter = encounter;
        _ownership = ownership;
        _commander = commander;
        _gameMenu = gameMenu;
        _logger = logger;
    }

    public event Action<DischargeReason> EnlistmentEnded;

    /// <summary>
    /// The single exit from service. Every step is ordered for a reason; see the inline notes
    /// before moving one. INV-D1 (pinned by tests) holds after this returns true, for EVERY reason.
    /// </summary>
    public bool Execute(DischargeReason reason)
    {
        // 1 — guard
        if (!_store.Record.IsEnlisted)
        {
            _logger?.LogWarning($"DischargeService: Execute({reason}) ignored — not enlisted (state {_store.Record.State})");
            return false;
        }

        // 2 — atomic state
        if (!_machine.TryTransition(EnlistmentState.Discharging))
            _logger?.LogError($"DischargeService: transition to Discharging failed from {_store.Record.State}; forcing pipeline");

        // 3 — CAPTURE before anything clears the record. Step 8 wipes CommanderHeroId, so reading
        // it later silently yields nothing — which is exactly how distToCommander printed "?" in
        // every discharge line.
        var commanderHeroId = _store.Record.CommanderHeroId;
        // Never let a null snapshot throw here: this pipeline must complete for EVERY reason,
        // including ones raised precisely because the commander is gone.
        var commander = _commander.GetSnapshot(commanderHeroId) ?? CommanderSnapshot.Missing;

        // 4 — begin
        var before = _attachment.GetPresence(commanderHeroId);
        _logger?.LogInfo($"[EnlistDiag] DISCHARGE({reason}) begin | {before?.Describe() ?? "presence unavailable"}");

        // 5 — presence FIRST. SetAttachedToInternal only tears down the inherited MapEventSide
        // while the party is active, so step 6 has to follow this rather than precede it.
        if (!_attachment.RestorePresence())
            _logger?.LogError($"DischargeService: RestorePresence failed during discharge ({reason}) — continuing pipeline");

        // 6 — detach BEFORE the encounter work: PlayerEncounter.Finish branches on Army/AttachedTo.
        _attachment.ClearArmyAttachment();

        // 7 — encounter, per the ownership policy. Discharge outranks ownership: leaving one live
        // is the save-breaker, because EncounterManager refuses every main-party encounter while
        // PlayerEncounter.Current is set.
        var verdict = _ownership.Evaluate(
            EncounterFinishIntent.Discharge, _encounter.GetOwnership(commander.PartyId));
        if (verdict == EncounterFinishVerdict.Finish)
        {
            _logger?.LogWarning($"[EnlistDiag] DISCHARGE({reason}) finishing a live PlayerEncounter — otherwise the player could never interact again");
            if (!_encounter.Finish(true))
                _logger?.LogError($"[EnlistDiag] DISCHARGE({reason}) could not finish the lingering PlayerEncounter — the player may be unable to talk to anyone");
        }
        else if (verdict != EncounterFinishVerdict.NothingToFinish)
        {
            _logger?.LogInfo($"[EnlistDiag] DISCHARGE({reason}) left the live encounter alone: {verdict}");
        }

        // 8 — clear
        _attachment.InvalidateCommanderCache();
        if (!_machine.TryTransition(EnlistmentState.NotEnlisted))
            _store.Record.State = EnlistmentState.NotEnlisted;
        _store.Record.Reset();

        // 9 — subscribers BEFORE placement. The content layer cancels the active duty here, and
        // step 10's EnterSettlementAction dispatches OnSettlementEntered, which the duty runtime
        // treats as a COMPLETION trigger. Reversing these two would complete a cancelled duty.
        try
        {
            EnlistmentEnded?.Invoke(reason);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"DischargeService: EnlistmentEnded subscriber threw for {reason}: {ex.Message}");
        }

        // 10 — hand the player back somewhere they can act
        RestoreCampaignContext(reason, commander);

        // 11 — verify, and distinguish the benign shape from the save-breaker
        var after = _attachment.GetPresence(commanderHeroId);
        _logger?.LogInfo($"[Enlistment] service ended: {reason}");
        _logger?.LogInfo($"[EnlistDiag] DISCHARGE({reason}) end | {after?.Describe() ?? "presence unavailable"}");

        if (after != null && after.EncountersBlocked)
        {
            var settlementOnly = after.IsHeldInsideSettlement
                && after.IsActive && !after.IsInMapEvent && !after.HasPlayerEncounter && !after.IsAttachedToParty;

            if (settlementOnly)
            {
                // Expected after a release INSIDE a town: the player has a settlement menu and can
                // walk out normally. Not the stranded shape.
                _logger?.LogWarning($"[EnlistDiag] DISCHARGE({reason}) left the player inside '{after.SettlementId}' — normal for a release in a settlement, they can leave from the menu.");
            }
            else
            {
                _logger?.LogError(
                    $"[EnlistDiag] DISCHARGE({reason}) LEFT THE PLAYER UNABLE TO START ENCOUNTERS — {after.Describe()}. " +
                    "They will not be able to click lords or settlements. This is a bug; report this line.");
            }
        }

        return true;
    }

    /// <summary>
    /// Put the player somewhere usable. The donor releases them into the commander's settlement;
    /// simply un-hiding them wherever the park left them is how a discharge produced a player
    /// standing on the map with no menu.
    /// </summary>
    private void RestoreCampaignContext(DischargeReason reason, CommanderSnapshot commander)
    {
        // Load-path reasons never move the player — the SAVED position is authoritative, and moving
        // them during normalization would teleport them on every load.
        if (reason == DischargeReason.HeirSuccessionOrPossessionMismatch)
        {
            ExitServiceMenuIfOpen(reason);
            return;
        }

        var settlementId = commander.SettlementId;
        var menuId = commander.SettlementMenuId;
        if (!string.IsNullOrEmpty(settlementId) && !string.IsNullOrEmpty(menuId))
        {
            if (_attachment.MoveIntoSettlement(settlementId) && _gameMenu.EnsureMenuOpen(menuId))
            {
                _logger?.LogInfo($"[EnlistDiag] DISCHARGE({reason}) released the player into '{settlementId}' ({menuId})");
                return;
            }

            _logger?.LogError($"[EnlistDiag] DISCHARGE({reason}) could not open '{menuId}' for '{settlementId}' — leaving the settlement rather than stranding the player inside it");
        }

        // EVERY path that did not just open a real settlement menu must walk the player OUT of
        // whatever settlement they are standing in. This used to live inside the branch above,
        // keyed on where the COMMANDER is — so a discharge while the player was inside a settlement
        // and the commander was NOT (dead, in the field, in a hideout) skipped it entirely and then
        // closed the wait menu, leaving them with CurrentSettlement set and no menu at all.
        //
        // That is terminal, not cosmetic. MobileParty.DoUpdatePosition refuses to move a party with
        // CurrentSettlement set; CheckExitingSettlementParallel explicitly skips the main party; and
        // the menu the engine re-pushes for a fortification is "town_outside", whose Leave option
        // calls PlayerEncounter.Finish() — which returns immediately when Current is null and never
        // reaches its own LeaveSettlement(). For a village the engine pushes nothing at all. It
        // survives save/reload, because the record now reads NotEnlisted and every recovery loop in
        // this feature early-returns on exactly that.
        var presence = _attachment.GetPresenceFlags();
        if (presence.IsInSettlement)
        {
            _logger?.LogWarning($"[EnlistDiag] DISCHARGE({reason}) is leaving '{presence.SettlementId}' — the player was inside it and no settlement menu was opened for them");
            if (!_attachment.LeaveSettlement())
                _logger?.LogError($"[EnlistDiag] DISCHARGE({reason}) COULD NOT leave '{presence.SettlementId}' — the player is immobile inside a settlement with no menu. This is a soft-lock; report this line.");
        }

        ExitServiceMenuIfOpen(reason);
    }

    /// <summary>
    /// Leave the service wait menu — GATED, because <c>GameMenu.ExitToLast</c> sets
    /// <c>TimeControlMode = Stop</c> unconditionally before delegating to a null-guarded manager.
    /// Calling it with no menu open freezes campaign time with nothing on screen.
    /// </summary>
    private void ExitServiceMenuIfOpen(DischargeReason reason)
    {
        if (_gameMenu.CurrentMenuId != EnlistmentMenuService.ServiceWaitMenuId)
            return;

        if (_gameMenu.ExitToLast())
            _logger?.LogInfo($"[EnlistDiag] DISCHARGE({reason}) left the service wait menu");
        else
            _logger?.LogError($"[EnlistDiag] DISCHARGE({reason}) could not leave the service wait menu — the player may be stuck in an optionless menu");
    }
}
