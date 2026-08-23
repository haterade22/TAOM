using System;
using System.Collections.Generic;
using TAOM.Core.Logging;
using TAOM.Features.SupplyLines.Domain;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace TAOM.Features.SupplyLines.Hooks;

/// <summary>
/// Thin boundary (ADR-002) for SupplyLines: wires campaign events and the town menu option to
/// <see cref="ISupplyOrderService"/> and persists the order book via SyncData. All decisions live
/// in the services.
///
/// <para>The hourly tick is deliberately NOT gated on the master toggle: turning the feature off
/// mid-transit must still let existing caravans finish so cargo the player already paid for is not
/// stranded. Only the per-frame movement pass and the menu option are gated.</para>
/// </summary>
public sealed class SupplyLinesCampaignBehavior : CampaignBehaviorBase
{
    private const string MenuOptionId = "taom_sl_order_supplies";

    private readonly ISupplyOrderService _orders;
    private readonly ISupplyLinesSettingsProvider _settings;
    private readonly ISupplyRouteVisualService _routeVisual;
    private readonly IModLogger _logger;

    private bool _frameTickFaulted;

    // True only after SyncData ran on a LOADING data store, i.e. this session installed a saved
    // order book. OnSessionLaunched reads it: a session that never loaded a record (fresh
    // campaign, or a save written before the feature existed) must reset the process-singleton
    // service or the previous campaign's orders ride along (round-A CRITICAL / Codex P1).
    private bool _syncedThisSession;

    public SupplyLinesCampaignBehavior(
        ISupplyOrderService orders,
        ISupplyLinesSettingsProvider settings,
        ISupplyRouteVisualService routeVisual,
        IModLogger logger)
    {
        _orders = orders;
        _settings = settings;
        _routeVisual = routeVisual;
        _logger = logger;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
        CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
        CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
        // A caravan destroyed by an AI battle must record its loss IMMEDIATELY, not at the next
        // hourly verdict: an autosave in that window serializes a stale InTransit row, and the
        // load-side RespawnMissing would resurrect the whole convoy with its cargo (Codex #7).
        CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, OnMobilePartyDestroyed);
    }

    public override void SyncData(IDataStore dataStore)
    {
        if (dataStore.IsLoading)
        {
            Dictionary<string, SupplyOrder> orders = null;
            int counter = 0;
            dataStore.SyncData("_taomSupplyOrders", ref orders);
            dataStore.SyncData("_taomSupplyOrderCounter", ref counter);
            // LoadFrom also drops the caravan trackers: a tracker's cached party belongs to
            // the previous session, and OnGameLoaded rebinds from the loaded campaign.
            _orders.LoadFrom(orders, counter);
            _syncedThisSession = true;
        }
        else
        {
            _orders.SaveInto(out Dictionary<string, SupplyOrder> orders, out int counter);
            dataStore.SyncData("_taomSupplyOrders", ref orders);
            dataStore.SyncData("_taomSupplyOrderCounter", ref counter);
        }
    }

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        EnsureSessionInitialized();
        _routeVisual.ClearAll(); // a previous session's map entities must never leak into this one
        AddMenuOptions(starter);
    }

    /// <summary>
    /// The reset half of the session contract (internal for direct unit testing via
    /// InternalsVisibleTo): when SyncData never ran on a loading store this session, the
    /// singleton book still holds the PREVIOUS session's orders; both the fresh-campaign path
    /// and the record-less-save path land here and start empty.
    /// </summary>
    internal void EnsureSessionInitialized()
    {
        if (_syncedThisSession)
            return;
        _orders.ResetForNewSession();
        // The singleton book now belongs to this session (empty); a stray second call must not
        // wipe orders placed after launch.
        _syncedThisSession = true;
    }

    private void AddMenuOptions(CampaignGameStarter starter)
    {
        var text = new TextObject("{=taom_sl_order}Order Supplies").ToString();
        starter.AddGameMenuOption("town", MenuOptionId, text, MenuCondition, MenuConsequence);
        starter.AddGameMenuOption("town_keep", MenuOptionId, text, MenuCondition, MenuConsequence);
    }

    private bool MenuCondition(MenuCallbackArgs args)
    {
        args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
        if (!_settings.Enabled)
            return false;
        return Settlement.CurrentSettlement?.IsUnderSiege != true;
    }

    private void MenuConsequence(MenuCallbackArgs args)
    {
        UI.SupplyOrderScreens.Open();
    }

    private void OnHourlyTick()
    {
        try
        {
            _orders.HourlyTick();
        }
        catch (Exception ex)
        {
            _logger.LogError($"[SupplyLines] HourlyTick threw: {ex}");
        }
    }

    private void OnTick(float dt)
    {
        try
        {
            if (_settings.Enabled)
                _orders.FrameTick();
            _routeVisual.Update(); // gates itself on ShowRouteVisual, with a clear-once latch
        }
        catch (Exception ex)
        {
            if (!_frameTickFaulted)
            {
                _frameTickFaulted = true; // per-frame handler: log the first failure, not 60/second
                _logger.LogError($"[SupplyLines] frame tick threw (logged once): {ex}");
            }
        }
    }

    private void OnMobilePartyDestroyed(
        TaleWorlds.CampaignSystem.Party.MobileParty party, TaleWorlds.CampaignSystem.Party.PartyBase destroyer)
    {
        try
        {
            if (party?.PartyComponent is Components.SupplyCaravanComponent component)
                _orders.OnCaravanDestroyed(component.OrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[SupplyLines] MobilePartyDestroyed handler threw: {ex}");
        }
    }

    private void OnGameLoaded(CampaignGameStarter starter)
    {
        try
        {
            HandleGameLoaded();
        }
        catch (Exception ex)
        {
            _logger.LogError($"[SupplyLines] OnGameLoaded threw: {ex}");
        }
    }

    /// <summary>
    /// OnGameLoaded dispatches BEFORE OnSessionLaunched (verified v1.4.8: LoadBehaviorData →
    /// OnGameLoadedEvent → OnSessionLaunchedEvent), so the reset gate must run here too: loading
    /// a record-less save with the previous session's book still installed would otherwise
    /// RESPAWN that session's caravans into this campaign before the launch handler ever gets to
    /// reset the book. Internal for direct unit testing (InternalsVisibleTo).
    /// </summary>
    internal void HandleGameLoaded()
    {
        EnsureSessionInitialized();
        _orders.OnGameLoaded();
    }
}
