using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.Core;
using TAOM.Adapters;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment.Domain;

using TAOM.Features.Enlistment.Presentation;
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
    /// <summary>Nominal seconds per wait-menu frame; the pump's shared budget does the real throttling.</summary>
    private const float WaitMenuFrameSeconds = 1f / 30f;

    private readonly IEnlistmentStore _store;
    private readonly IServiceAttachmentService _attachment;
    private readonly IEnlistmentWaitMenuPresenter _presenter;
    private readonly IGameMenuAdapter _gameMenu;
    private readonly ICoopSessionProvider _coopSession;
    private readonly IServiceMaintenanceService _maintenance;
    private readonly IEnlistmentWaitMenuOptions _options;

    public EnlistmentMenuBehavior(
        IEnlistmentStore store,
        IServiceAttachmentService attachment,
        IEnlistmentWaitMenuPresenter presenter,
        IGameMenuAdapter gameMenu,
        ICoopSessionProvider coopSession,
        IServiceMaintenanceService maintenance,
        IEnlistmentWaitMenuOptions options)
    {
        _store = store;
        _attachment = attachment;
        _presenter = presenter;
        _gameMenu = gameMenu;
        _coopSession = coopSession;
        _maintenance = maintenance;
        _options = options;
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

        _options.Register(starter);
    }

    private void OnWaitMenuInit(MenuCallbackArgs args)
    {
        // Post-battle closure: the loot flow ends with a redirected menu push landing
        // here — re-assert the park event-driven instead of waiting for the hourly tick.
        //
        // NOT while inside a settlement. Parking is "hide outside next to the commander", so an
        // unconditional park here yanks the player straight back out of the town they just
        // followed him into — and this menu is asserted BY the follow transaction, so it would
        // undo itself on the very next frame.
        if (_coopSession.IsAuthority
            && _store.Record.State == EnlistmentState.EnlistedAttached
            && !_attachment.GetPresenceFlags().IsInSettlement)
        {
            _attachment.EnsureParked(_store.Record.CommanderHeroId);
        }

        _presenter.RefreshWaitText();
    }

    private void OnWaitMenuTick(MenuCallbackArgs args, CampaignTime dt)
    {
        // Second source for the SAME pump, sharing its budget — adding it cannot double the work
        // rate. It exists because CampaignEvents.TickEvent is gated on `_dt > 0f` and the menu
        // system sets TimeControlMode = Stop, so while the player sits on the wait menu the
        // campaign tick can be silent. The pump owns the throttle now; this behaviour owns none.
        if (!_coopSession.IsAuthority)
            return;

        _maintenance.Pump(WaitMenuFrameSeconds, CampaignTime.Now.ToHours);
    }

    // Asking to leave is a DECISION with a cost the player must see first, so the whole thing —
    // verdict, popup, discharge — lives in the presenter. The menu exit is DischargeService's
    // job now (INV-D1); calling ExitToLast here as well would stop campaign time on the refusal
    // paths, where nothing was discharged at all.

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
