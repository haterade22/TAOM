using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.Core;
using TAOM.Adapters;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Features.Enlistment.Hooks;

/// <summary>
/// Thin registration behavior (ADR-002) for the service wait menu. The engine ticks the
/// wait menu itself — that tick drives the parked position sync (throttled; no per-frame
/// allocation, text is refreshed only on menu init via the presenter). Menu-guard policy
/// lives in <see cref="IEnlistmentMenuService"/>; terminal decisions stay with the hourly
/// reconciler. Stateless (no SyncData).
/// </summary>
public class EnlistmentMenuBehavior : CampaignBehaviorBase
{
    private const int PositionSyncTickInterval = 5;

    private readonly IEnlistmentStore _store;
    private readonly IServiceAttachmentService _attachment;
    private readonly IEnlistmentService _service;
    private readonly IEnlistmentDialogGateService _gate;
    private readonly IEnlistmentWaitMenuPresenter _presenter;
    private readonly IGameMenuAdapter _gameMenu;
    private readonly ICoopSessionProvider _coopSession;

    private int _tickCounter;

    public EnlistmentMenuBehavior(
        IEnlistmentStore store,
        IServiceAttachmentService attachment,
        IEnlistmentService service,
        IEnlistmentDialogGateService gate,
        IEnlistmentWaitMenuPresenter presenter,
        IGameMenuAdapter gameMenu,
        ICoopSessionProvider coopSession)
    {
        _store = store;
        _attachment = attachment;
        _service = service;
        _gate = gate;
        _presenter = presenter;
        _gameMenu = gameMenu;
        _coopSession = coopSession;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        CampaignEvents.ConversationEnded.AddNonSerializedListener(this, OnConversationEnded);
    }

    public override void SyncData(IDataStore dataStore) { }

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        starter.AddWaitGameMenu(
            EnlistmentMenuService.ServiceWaitMenuId,
            "{TAOM_ENLISTMENT_WAIT_TEXT}",
            OnWaitMenuInit,
            args => _store.Record.IsEnlisted,
            null,
            OnWaitMenuTick,
            GameMenu.MenuAndOptionType.WaitMenuHideProgressAndHoursOption,
            GameMenu.MenuOverlayType.None,
            0f,
            GameMenu.MenuFlags.None,
            null);

        starter.AddGameMenuOption(
            EnlistmentMenuService.ServiceWaitMenuId,
            "taom_enlist_menu_status",
            "{=taom_enlist_menu_status}Review your service",
            args => { args.optionLeaveType = GameMenuOption.LeaveType.Manage; return true; },
            _ => _presenter.ShowServiceStatus(),
            false, 0);

        starter.AddGameMenuOption(
            EnlistmentMenuService.ServiceWaitMenuId,
            "taom_enlist_menu_leave",
            "{=taom_enlist_menu_leave}Ask to be released from service",
            args => { args.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
            _ => OnLeaveServiceSelected(),
            false, 1);
    }

    private void OnWaitMenuInit(MenuCallbackArgs args)
    {
        // Post-battle closure: the loot flow ends with a redirected menu push landing
        // here — re-assert the park event-driven instead of waiting for the hourly tick.
        _tickCounter = 0;
        if (_coopSession.IsAuthority && _store.Record.State == EnlistmentState.EnlistedAttached)
            _attachment.EnsureParked(_store.Record.CommanderHeroId);
        _presenter.RefreshWaitText();
    }

    private void OnWaitMenuTick(MenuCallbackArgs args, CampaignTime dt)
    {
        // Parked position sync, throttled (the donor burned a 4 Hz global tick on this).
        if (!_coopSession.IsAuthority || ++_tickCounter % PositionSyncTickInterval != 0)
            return;
        if (_store.Record.State == EnlistmentState.EnlistedAttached)
            _attachment.SyncPosition(_store.Record.CommanderHeroId);
    }

    private void OnLeaveServiceSelected()
    {
        // CO-OP: host-only, like every other discharge path. A client running this locally
        // would restore its own presence and clear its record while the host stayed
        // enlisted (Codex P2-3).
        if (!_coopSession.IsAuthority)
            return;

        // Terminal decision routed through the single discharge pipeline. Leaving before
        // the contract day classifies as desertion (arrears forfeit + relation cost in
        // the consequence layer); at/after it, an honorable release.
        var reason = _gate.ClassifyLeaveReason(CampaignTime.Now.ToDays);
        if (_service.RequestDischarge(reason))
            _gameMenu.ExitToLast();
    }

    private void OnConversationEnded(IEnumerable<CharacterObject> characters)
    {
        // Replaces the donor's MapState.OnMapConversationOver patch: after any map
        // conversation while attached, re-assert the wait menu.
        if (!_coopSession.IsAuthority || _store.Record.State != EnlistmentState.EnlistedAttached)
            return;
        if (_gameMenu.CurrentMenuId != EnlistmentMenuService.ServiceWaitMenuId)
            _gameMenu.Activate(EnlistmentMenuService.ServiceWaitMenuId);
    }
}
