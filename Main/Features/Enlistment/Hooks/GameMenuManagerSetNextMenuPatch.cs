using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.GameMenus;
using TAOM.Core.Logging;

namespace TAOM.Features.Enlistment.Hooks;

/// <summary>
/// Patch66 — the load-bearing menu guard. Vanilla pushes menus imperatively through
/// <c>GameMenuManager.SetNextMenu(string)</c> (verified 1.4.7: the sole statement is
/// <c>NextGameMenuId = name</c>); there is no event to veto, so a prefix rewriting the
/// ref parameter is the only interception point. All policy lives in
/// <see cref="IEnlistmentMenuService"/> — one state read + one HashSet probe when not
/// enlisted, and the patch fails open on any internal error (menu transitions must never
/// break on our account).
/// </summary>
[HarmonyPatch(typeof(GameMenuManager), "SetNextMenu")]
[HarmonyPatchCategory("Patch66_Enlistment")]
public static class GameMenuManagerSetNextMenuPatch
{
    private static IEnlistmentMenuService _menuService;
    private static bool _faultLogged;

    public static void Prefix(ref string name)
    {
        try
        {
            _menuService = _menuService ?? IoC.Resolve<IEnlistmentMenuService>();
            if (_menuService.TryRedirectMenu(name, out var redirected))
                name = redirected;
        }
        catch (Exception ex)
        {
            if (!_faultLogged)
            {
                _faultLogged = true;
                try { IoC.Resolve<IModLogger>().LogError($"[Enlistment] SetNextMenu prefix failed (failing open): {ex.Message}"); }
                catch { /* fail open — never break menu transitions */ }
            }
        }
    }
}
