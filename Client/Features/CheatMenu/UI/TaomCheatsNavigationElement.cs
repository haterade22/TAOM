using SandBox.View.Map.Navigation;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using TAOM.Core.Logging;

namespace TAOM.Features.CheatMenu.UI;

/// <summary>
/// The "Cheats" button on the campaign map's navigation bar (docs/features/cheat-menu.md). It opens the cheat menu through
/// the shared <see cref="CheatMenuOpener"/>. MapBar.xml renders every element generically (IconID="@ItemId"), so its art is a
/// <c>taom_cheats</c> layer in the two MapBar.Left brushes, on the game's own gold icon. IsActive stays false: the menu is a
/// game menu, not a pushed state (as the Fiefs button).
/// </summary>
public sealed class TaomCheatsNavigationElement : MapNavigationElementBase
{
    private readonly CheatMenuOpener _opener;
    private readonly IModLogger _logger;
    private readonly TextObject _label = new TextObject("{=taom_cheat_nav_label}Cheats");
    private readonly TextObject _busyReason = new TextObject("{=taom_cheat_nav_busy}The cheat menu opens from the free campaign map.");
    private bool _errorLogged;

    public TaomCheatsNavigationElement(MapNavigationHandler handler, CheatMenuOpener opener, IModLogger logger) : base(handler)
    {
        _opener = opener;
        _logger = logger;
    }

    public override string StringId => CheatMenuIds.NavigationElementId;

    public override bool IsActive => false;

    public override bool IsLockingNavigation => false;

    public override bool HasAlert => false;

    // Polled every frame by MapNavigationItemVM.RefreshStates. Never throws.
    protected override NavigationPermissionItem GetPermission()
    {
        try
        {
            if (!MapNavigationHelper.IsNavigationBarEnabled(_handler))
                return new NavigationPermissionItem(false, null);
            return _opener.GetAvailability() switch
            {
                CheatMenuAvailability.Available => new NavigationPermissionItem(true, null),
                CheatMenuAvailability.MapBusy => new NavigationPermissionItem(false, _busyReason),
                _ => new NavigationPermissionItem(false, null),
            };
        }
        catch (System.Exception ex)
        {
            LogOnce("GetPermission", ex);
            return new NavigationPermissionItem(false, null);
        }
    }

    protected override TextObject GetTooltip() => _label;

    protected override TextObject GetAlertTooltip() => TextObject.GetEmpty();

    public override void OpenView()
    {
        try { _opener.TryOpen(); }
        catch (System.Exception ex) { LogOnce("OpenView", ex); }
    }

    public override void OpenView(params object[] parameters) => OpenView();

    public override void GoToLink()
    {
        // No encyclopedia page for the cheat menu.
    }

    private void LogOnce(string where, System.Exception ex)
    {
        if (_errorLogged) return;
        _errorLogged = true;
        try { _logger.LogError($"[CheatMenu] TaomCheatsNavigationElement.{where} threw: {ex}"); }
        catch { /* swallow-ok: a UI poll must never turn into a crash */ }
    }
}
