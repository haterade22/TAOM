using TAOM.Features.CoopInterop;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace TAOM.Features.SupplyLines.Hooks;

/// <summary>
/// The town and keep menu option that opens the supply order screen. A co-op guest sees it disabled with
/// its reason: an order spawns a caravan and moves gold, shared state the host owns (#1086).
/// </summary>
internal sealed class SupplyOrderTownMenu
{
    private const string MenuOptionId = "taom_sl_order_supplies";

    private readonly ISupplyLinesSettingsProvider _settings;
    private readonly ICoopSessionProvider _coop;

    internal SupplyOrderTownMenu(ISupplyLinesSettingsProvider settings, ICoopSessionProvider coop)
    {
        _settings = settings;
        _coop = coop;
    }

    internal void AddTo(CampaignGameStarter starter)
    {
        var text = new TextObject("{=taom_sl_order}Order Supplies").ToString();
        starter.AddGameMenuOption("town", MenuOptionId, text, Condition, Consequence);
        starter.AddGameMenuOption("town_keep", MenuOptionId, text, Condition, Consequence);
    }

    internal bool Condition(MenuCallbackArgs args)
    {
        args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
        if (!_settings.Enabled)
            return false;
        if (_coop.ShouldDeferToHost)
        {
            args.IsEnabled = false;
            args.Tooltip = new TextObject("{=taom_sl_coop_off}Supply orders are not available in a shared campaign.");
            return true;
        }
        return Settlement.CurrentSettlement?.IsUnderSiege != true;
    }

    private static void Consequence(MenuCallbackArgs args) => UI.SupplyOrderScreens.Open();
}
