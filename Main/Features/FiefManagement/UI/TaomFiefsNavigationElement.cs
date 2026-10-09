using SandBox.View.Map.Navigation;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Localization;
using TAOM.Core.Logging;

namespace TAOM.Features.FiefManagement.UI;

/// <summary>
/// The "Fiefs" button on the campaign map's navigation bar (#789). It opens the same fief hub as F6
/// by calling the shared <see cref="FiefHubOpener"/>. MapBar.xml renders every element generically
/// (IconID="@ItemId"), so the only art it needs is a <c>taom_fiefs</c> layer in the two MapBar.Left
/// brushes in Main/_Module/GUI/Brushes/MapBar.xml.
///
/// IsActive stays false on purpose: fief_hub is a game menu, not a pushed state, so an active element
/// would leak the bar's selected look onto the screens pushed from it. Constructed by the postfix on
/// <c>MapNavigationHandler.OnCreateElements</c> (see Patch36_MapNavigationElements), which resolves the
/// opener and logger and hands them in.
/// </summary>
public sealed class TaomFiefsNavigationElement : MapNavigationElementBase
{
    public const string ElementId = "taom_fiefs";

    // F6 is a raw InputKey with no game-key binding to look up (MapScreenInputAdapter reads it directly);
    // the literal matches every shipped translation of vanilla's str_game_key_text.f6.
    private const string HotkeyLabel = "F6";

    private readonly FiefHubOpener _opener;
    private readonly IModLogger _logger;
    private readonly TextObject _label = new TextObject("{=taom_fief_nav_label}Fiefs");
    private readonly TextObject _featureOffReason = new TextObject("{=taom_fief_nav_disabled}Fief management is turned off in the mod options.");
    private readonly TextObject _mapBusyReason = new TextObject("{=taom_fief_nav_busy}The fief hub cannot be opened right now.");
    private readonly TextObject _noFiefsReason = new TextObject("{=taom_fief_no_fiefs}You don't own any fiefs yet.");
    private bool _errorLogged;

    public TaomFiefsNavigationElement(MapNavigationHandler handler, FiefHubOpener opener, IModLogger logger) : base(handler)
    {
        _opener = opener;
        _logger = logger;
    }

    public override string StringId => ElementId;

    public override bool IsActive => false;

    public override bool IsLockingNavigation => false;

    public override bool HasAlert => false;

    // Polled every frame by MapNavigationItemVM.RefreshStates: vanilla's bar check first, then the
    // opener's cheap availability query. Never throws.
    protected override NavigationPermissionItem GetPermission()
    {
        try
        {
            if (!MapNavigationHelper.IsNavigationBarEnabled(_handler))
                return new NavigationPermissionItem(false, null);

            switch (_opener.GetAvailability())
            {
                case FiefHubAvailability.Available:
                    return new NavigationPermissionItem(true, null);
                case FiefHubAvailability.FeatureDisabled:
                    return new NavigationPermissionItem(false, _featureOffReason);
                case FiefHubAvailability.MapBusy:
                    return new NavigationPermissionItem(false, _mapBusyReason);
                case FiefHubAvailability.NoFiefs:
                    return new NavigationPermissionItem(false, _noFiefsReason);
                default:
                    return new NavigationPermissionItem(false, null);
            }
        }
        catch (System.Exception ex)
        {
            LogOnce("GetPermission", ex);
            return new NavigationPermissionItem(false, null);
        }
    }

    protected override TextObject GetTooltip()
    {
        if (!Input.IsGamepadActive && GetPermission().IsAuthorized)
        {
            var hint = GameTexts.FindText("str_hotkey_with_hint");
            hint.SetTextVariable("TEXT", _label.ToString());
            hint.SetTextVariable("HOTKEY", HotkeyLabel);
            return hint;
        }
        return _label;
    }

    protected override TextObject GetAlertTooltip() => TextObject.GetEmpty();

    public override void OpenView()
    {
        try
        {
            if (!GetPermission().IsAuthorized) return;
            _opener.TryOpen("nav button");
        }
        catch (System.Exception ex)
        {
            LogOnce("OpenView", ex);
        }
    }

    public override void OpenView(params object[] parameters) => OpenView();

    public override void GoToLink()
    {
        // No encyclopedia page for the fief hub.
    }

    private void LogOnce(string where, System.Exception ex)
    {
        if (_errorLogged) return;
        _errorLogged = true;
        try { _logger.LogError($"[FiefManagement] TaomFiefsNavigationElement.{where} threw: {ex}"); }
        catch { /* the logger must never turn a UI poll into a crash */ }
    }
}
