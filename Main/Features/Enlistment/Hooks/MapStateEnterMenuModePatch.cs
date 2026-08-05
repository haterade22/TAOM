using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.GameMenus;
using TAOM.Core.Logging;

namespace TAOM.Features.Enlistment.Hooks;

/// <summary>
/// Patch66 — recovery hook for menu ids that reached the map state WITHOUT passing
/// through <c>GameMenuManager.SetNextMenu</c> (some native paths write
/// <c>MapStateData.GameMenuId</c> directly). After menu mode is entered, if the active
/// menu is one the redirect policy owns, switch to the service wait menu. Fails open.
/// </summary>
[HarmonyPatch(typeof(MapState), "EnterMenuMode")]
[HarmonyPatchCategory("Patch66_Enlistment")]
public static class MapStateEnterMenuModePatch
{
    private static IEnlistmentMenuService _menuService;
    private static bool _faultLogged;

    public static void Postfix(MapState __instance)
    {
        try
        {
            _menuService = _menuService ?? IoC.Resolve<IEnlistmentMenuService>();
            if (_menuService.TryRedirectMenu(__instance.GameMenuId, out var redirected))
                GameMenu.SwitchToMenu(redirected);
        }
        catch (Exception ex)
        {
            if (!_faultLogged)
            {
                _faultLogged = true;
                try { IoC.Resolve<IModLogger>().LogError($"[Enlistment] EnterMenuMode postfix failed (failing open): {ex.Message}"); }
                catch { /* fail open */ }
            }
        }
    }
}
