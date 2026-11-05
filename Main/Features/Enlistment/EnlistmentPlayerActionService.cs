using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment.Domain;
using TAOM.Features.Enlistment.Duties;

namespace TAOM.Features.Enlistment;

public class EnlistmentPlayerActionService : IEnlistmentPlayerActionService
{
    private readonly IEnlistmentStore _store;
    private readonly ICommanderLordAdapter _commander;
    private readonly IMapConversationAdapter _conversation;
    private readonly IDutyOrchestrationService _duties;
    private readonly ICoopSessionProvider _coopSession;
    private readonly IMobilePartyAttachmentAdapter _attachment;
    private readonly IEncounterAdapter _encounter;
    private readonly IGameMenuAdapter _gameMenu;
    private readonly IModLogger _logger;

    public EnlistmentPlayerActionService(
        IEnlistmentStore store,
        ICommanderLordAdapter commander,
        IMapConversationAdapter conversation,
        IDutyOrchestrationService duties,
        ICoopSessionProvider coopSession,
        IMobilePartyAttachmentAdapter attachment,
        IEncounterAdapter encounter,
        IGameMenuAdapter gameMenu,
        IModLogger logger)
    {
        _store = store;
        _commander = commander;
        _conversation = conversation;
        _duties = duties;
        _coopSession = coopSession;
        _attachment = attachment;
        _encounter = encounter;
        _gameMenu = gameMenu;
        _logger = logger;
    }

    public TalkToCommanderResult CanTalkToCommander()
    {
        var record = _store.Record;
        if (!record.IsEnlisted)
            return TalkToCommanderResult.NotEnlisted;

        // Ordered by how badly getting it wrong hurts. InBattle first: opening a conversation
        // during a battle tears the PlayerEncounter the battle service seeded, and that encounter
        // is the ONLY thing advancing the player's map event (the engine ticks every map event
        // except the player's). Losing it freezes the battle with no way back.
        if (record.State == EnlistmentState.EnlistedBattle)
            return TalkToCommanderResult.InBattle;

        var commander = _commander.GetSnapshot(record.CommanderHeroId);
        if (commander == null || !commander.Exists || !commander.IsAlive
            || commander.IsPrisoner || !commander.HasParty || !commander.PartyIsActive)
        {
            return TalkToCommanderResult.CommanderUnavailable;
        }

        if (!_conversation.CanOpenConversation)
            return TalkToCommanderResult.NotOnMap;

        return TalkToCommanderResult.Opened;
    }

    public TalkToCommanderResult TalkToCommander()
    {
        // Re-run the gate. A menu option's condition and its consequence are separated by at least
        // a frame, and a commander battle starting in that gap is exactly the case where acting on
        // the stale answer costs the player the whole fight.
        var verdict = CanTalkToCommander();
        if (verdict != TalkToCommanderResult.Opened)
        {
            _logger?.LogInfo($"[Enlistment] talk-to-commander declined: {verdict}");
            return verdict;
        }

        if (_conversation.OpenWithHero(_store.Record.CommanderHeroId))
            return TalkToCommanderResult.Opened;

        _logger?.LogWarning($"[Enlistment] could not open a conversation with '{_store.Record.CommanderHeroId}' — the engine refused");
        return TalkToCommanderResult.CommanderUnavailable;
    }

    /// <summary>
    /// CO-OP: host-only. The daily duty tick is already host-gated; this explicit menu path
    /// reaches the same orchestration — starting field duties, spawning target parties,
    /// presenting reward-bearing inquiries and mutating the content record — so a client
    /// running it locally would fork shared campaign state.
    /// </summary>
    public DutyRequestResult RequestDutyNow(double nowDays, double hourOfDay)
    {
        if (!_coopSession.IsAuthority)
            return DutyRequestResult.NoWorkAvailable;

        return _duties.RequestDutyNow(nowDays, hourOfDay);
    }

    public bool CanTakeTownLeave()
        => TownLeavePolicy.CanTakeLeave(
            _store.Record.State, InsideSettlement(), _store.Record.OnTownLeave);

    public bool TakeTownLeave()
    {
        // Re-check rather than trust the condition that drew the option: a frame passes between a
        // menu option's condition and its consequence, and the column can start marching in it —
        // the same reasoning TalkToCommander documents above.
        if (!CanTakeTownLeave())
            return false;

        // THE PASS HANDS OVER A VANILLA SETTLEMENT MENU, so it must first make one safe to show.
        // The follow path walked the player in with EnterSettlementAction alone, so there is no
        // PlayerEncounter and no LocationEncounter. Issue #510.
        //
        // On THIS path the affordance that needs it is LocationEncounter: the tavern, the arena,
        // the keep and the town-centre walk all route through
        // PlayerEncounter.LocationEncounter.CreateAndOpenMissionController. "Wait here for some
        // time" is NOT the hazard here, because it switches to town_wait_menus, which is in
        // RedirectMenuIds and is still redirected while enlisted — only the three settlement menus
        // are released on a pass. That option is the crash on the DISCHARGE path, where the record
        // already reads NotEnlisted and the redirect no longer applies at all.
        //
        // A refused pass is a correct outcome. A pass into a CTD is not.
        var settlementId = SettlementId();
        if (!_encounter.EnsureSettlementEncounter(settlementId))
        {
            _logger?.LogError($"[Enlistment] shore leave REFUSED for '{settlementId ?? "-"}' — could not establish the settlement encounter its menu needs");
            return false;
        }

        _store.Record.OnTownLeave = true;
        _logger?.LogInfo("[Enlistment] shore leave granted — the settlement menu is the player's until the column moves");
        return true;
    }

    public bool IsServing => _store.Record.IsEnlisted;

    public void LeaveAfterDischarge()
    {
        if (IsServing)
        {
            // EndWait already ran before an isLeave consequence, and options redraw only on a menu
            // refresh, so re-enter the menu: SwitchTo restarts the wait.
            _gameMenu.SwitchTo(EnlistmentMenuService.ServiceWaitMenuId);
            return;
        }

        // The adapter catches its own read failures and answers "not in a settlement".
        var presence = _attachment.GetPresenceFlags();

        // Never open a town menu over a live battle: vanilla re-pushes its own menu for a map event.
        if (presence.IsInSettlement && !presence.IsInMapEvent)
        {
            // The load-time discharge left him in a town with no PlayerEncounter (#510 shape), so
            // the town menu is only safe once the encounter exists.
            var encounterReady = _encounter.EnsureSettlementEncounter(presence.SettlementId);
            if (encounterReady && _gameMenu.EnsureMenuOpen(_gameMenu.CurrentSettlementMenuId))
                return;

            // Same order as DischargeService.RestoreCampaignContext: close what we opened, then
            // walk out explicitly. Never leave him in a settlement with no menu.
            _logger?.LogWarning($"[Enlistment] discharged exit: could not open the menu for '{presence.SettlementId ?? "-"}', leaving the settlement");
            if (encounterReady && !_encounter.Finish(forcePlayerOutFromSettlement: true))
                _logger?.LogError("[Enlistment] discharged exit: could not finish the settlement encounter just opened; the player may be unable to start encounters");

            if (InsideSettlement() && !_attachment.LeaveSettlement())
            {
                _logger?.LogError($"[Enlistment] discharged exit: COULD NOT leave '{presence.SettlementId ?? "-"}'; keeping the service menu so the player still has an exit");
                // Finish may already have exited the menu; keep one with a working exit on screen.
                _gameMenu.EnsureMenuOpen(EnlistmentMenuService.ServiceWaitMenuId);
                return;
            }
        }

        // In a map event vanilla re-pushes 'encounter'. With no map event a live encounter has no
        // menu to advance it and blocks every new encounter, so that case is an error, not a note.
        if (_encounter.HasCurrent)
        {
            if (presence.IsInMapEvent)
                _logger?.LogWarning("[Enlistment] discharged exit: a player encounter is still live; vanilla re-pushes its menu for the map event");
            else
                _logger?.LogError("[Enlistment] discharged exit: a player encounter is still live with no map event; nothing will advance it and new encounters stay blocked");
        }

        _gameMenu.ExitToLast();
    }

    /// <summary>
    /// Presence read, guarded. This runs from a menu-option condition (every frame the wait menu is
    /// up) and from the maintenance pump, so a throw here would be a per-frame throw — and the
    /// honest answer when presence cannot be read is "not in a settlement", which closes the gate
    /// rather than opening it.
    /// </summary>
    private bool InsideSettlement()
    {
        try { return _attachment.GetPresenceFlags().IsInSettlement; }
        catch { return false; }
    }

    /// <summary>Guarded the same way, and for the same reason, as <see cref="InsideSettlement"/>.</summary>
    private string SettlementId()
    {
        try { return _attachment.GetPresenceFlags().SettlementId; }
        catch { return null; }
    }
}
