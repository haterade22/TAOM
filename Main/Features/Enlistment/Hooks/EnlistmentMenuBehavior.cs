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
/// wait menu itself — while campaign time is stopped that tick drives the whole pump (the pump also refreshes
/// the board every 2 budget seconds; no per-frame allocation, wait text is built on menu init via the
/// presenter). Menu-guard policy lives in <see cref="IEnlistmentMenuService"/>; terminal decisions stay
/// with the hourly reconciler. Stateless (no SyncData).
/// </summary>
public class EnlistmentMenuBehavior : CampaignBehaviorBase
{
    /// <summary>
    /// REAL elapsed time between wait-menu frames, measured rather than assumed; it feeds the pump only while time is stopped.
    ///
    /// This used to feed the pump a constant 1/30s per callback. But the wait-menu tick is a
    /// FRAME tick (GameMenuManager.OnFrameTick -> GameMenu.RunOnTick), so at 144 fps it fabricated
    /// ~4.8 seconds of budget per real second — driving the "4 Hz" expensive tier at ~19 Hz and
    /// the status board, which uses the explicitly forbidden GetSnapshot, at ~2.4 Hz. At 240 fps
    /// the board hit exactly the 4 Hz its own doc calls forbidden. The throttle was frame-rate
    /// dependent, which is the one thing a real-time budget must not be.
    /// </summary>
    private readonly System.Diagnostics.Stopwatch _waitMenuClock = System.Diagnostics.Stopwatch.StartNew();

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

        // Offer the pass while the column is ACTUALLY stopped. -= before += because RegisterEvents
        // runs once per session but this behavior outlives a campaign, and a plain += would stack a
        // second subscription on the next new game, popping the inquiry twice.
        _attachment.ColumnEnteredSettlement -= OnColumnEnteredSettlement;
        _attachment.ColumnEnteredSettlement += OnColumnEnteredSettlement;
        _attachment.ColumnLeftSettlement -= _presenter.OnStopEnded;   // once per stop, not per session
        _attachment.ColumnLeftSettlement += _presenter.OnStopEnded;
    }

    private void OnColumnEnteredSettlement(string settlementId) =>
        _presenter.OfferTownLeave(settlementId, CampaignTime.Now.ToHours);

    public override void SyncData(IDataStore dataStore) { }

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        starter.AddWaitGameMenu(
            EnlistmentMenuService.ServiceWaitMenuId,
            "{TAOM_ENLISTMENT_WAIT_TEXT}",
            OnWaitMenuInit,
            args => true, // #567, #828: never gate this; each option gates itself (EnlistmentServiceMenuWiringTests)
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
        // Every wait-menu frame (#841): first performs a raid join held from the campaign tick (this frame runs
        // before time advances), then pumps only while campaign time is stopped (TickEvent is silent then, and a
        // load lands here stopped). While time runs TickEvent already pumps. The pump owns the throttle.
        if (!_coopSession.IsAuthority)
            return;

        var elapsed = (float)_waitMenuClock.Elapsed.TotalSeconds;
        _waitMenuClock.Restart();

        _maintenance.OnWaitMenuFrame(dt.ToSeconds, elapsed, CampaignTime.Now.ToHours);
    }

    private void OnConversationEnded(IEnumerable<CharacterObject> characters)
    {
        // Replaces the donor's MapState.OnMapConversationOver patch: after any map
        // conversation while attached, re-assert the wait menu.
        if (!_coopSession.IsAuthority || _store.Record.State != EnlistmentState.EnlistedAttached)
            return;

        // SHORE LEAVE OUTRANKS THE INVARIANT HERE TOO, the same carve-out ServiceMaintenanceService
        // .EnsureServiceMenu makes and for the same reason. Without it, any conversation ending
        // while the player is on a pass (a lord in the keep, the tavern) slams the service menu
        // over the town menu he is entitled to — and since the pass is still held, the wait menu's
        // own "take leave" option is hidden by its !alreadyOnLeave condition, so he cannot get back
        // to the town until the column moves. Post-#510 that state also holds a live settlement
        // encounter behind a TAOM menu, which blocks the battle latch for its duration.
        if (_store.Record.OnTownLeave)
            return;
        if (_gameMenu.CurrentMenuId != EnlistmentMenuService.ServiceWaitMenuId)
            _gameMenu.Activate(EnlistmentMenuService.ServiceWaitMenuId);
    }
}
