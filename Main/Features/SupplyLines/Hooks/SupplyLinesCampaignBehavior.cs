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
    }

    public override void SyncData(IDataStore dataStore)
    {
        _orders.SaveInto(out Dictionary<string, SupplyOrder> orders, out int counter);
        dataStore.SyncData("_taomSupplyOrders", ref orders);
        dataStore.SyncData("_taomSupplyOrderCounter", ref counter);
        // On save this hands the same book straight back; on load it installs the loaded one.
        _orders.LoadFrom(orders, counter);
    }

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        _routeVisual.ClearAll(); // a previous session's map entities must never leak into this one
        AddMenuOptions(starter);
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

    private void OnGameLoaded(CampaignGameStarter starter)
    {
        try
        {
            _orders.OnGameLoaded();
        }
        catch (Exception ex)
        {
            _logger.LogError($"[SupplyLines] OnGameLoaded threw: {ex}");
        }
    }
}
