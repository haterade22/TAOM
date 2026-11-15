using System;
using System.Linq;
using HarmonyLib;
using SandBox.View.Map.Navigation;
using TaleWorlds.CampaignSystem;
using TAOM.Core.Logging;

namespace TAOM.Features.CheatMenu.Hooks;

/// <summary>
/// Adds the "Cheats" button to the campaign map's navigation bar while the cheat menu is shown (MCM switch on, single
/// player), as Patch36 adds "Fiefs" (docs/reference/harmony-patch-registry.md, "Patch151_CheatMenu"). Runs inside the
/// <c>MapNavigationHandler</c> constructor, so the bar is decided once per map screen: a switch flipped later shows on the
/// next map screen (a load). Never throws out of the constructor: a failure leaves the bar as vanilla built it.
/// </summary>
[HarmonyPatch(typeof(MapNavigationHandler), "OnCreateElements")]
[HarmonyPatchCategory(CheatMenuModule.PatchCategory)]
[ClientOnly]
public static class Patch151_CheatMenuNavigation
{
    private static CheatMenuOpener? _opener;
    private static IModLogger? _logger;
    private static bool _exceptionLogged;

    internal static void Initialize(CheatMenuOpener opener, IModLogger logger)
    {
        _opener = opener;
        _logger = logger;
    }

    [HarmonyPostfix]
    public static void Postfix(MapNavigationHandler __instance, ref INavigationElement[] __result)
    {
        try
        {
            var opener = _opener;
            if (opener == null) return;
            // The factory is resolved at use, as Patch36 does: TAOM.Client replaces the core's null factory after the statics run.
            __result = AppendWhenShown(__result, opener.IsShown,
                () => IoC.Resolve<ICheatMenuNavigationElementFactory>().Create(__instance));
        }
        catch (Exception ex)
        {
            if (_exceptionLogged) return;
            _exceptionLogged = true;
            try { _logger?.LogError($"[CheatMenu] Patch151_CheatMenuNavigation.Postfix threw: {ex}"); }
            catch { /* swallow-ok: never throw out of a handler constructor */ }
        }
    }

    /// <summary>
    /// Pure: the input unchanged when it is null, when the menu is not shown, when it already holds the button or when the
    /// factory returns null; otherwise the input with the button appended.
    /// </summary>
    internal static INavigationElement[] AppendWhenShown(INavigationElement[] elements, bool shown, Func<INavigationElement?> factory)
    {
        if (elements == null || !shown) return elements!;
        if (elements.Any(e => e != null && e.StringId == CheatMenuIds.NavigationElementId)) return elements;

        var ours = factory();
        if (ours == null) return elements;

        var result = new INavigationElement[elements.Length + 1];
        Array.Copy(elements, result, elements.Length);
        result[elements.Length] = ours;
        return result;
    }
}
