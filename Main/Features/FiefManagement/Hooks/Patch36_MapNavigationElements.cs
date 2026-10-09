using System;
using System.Linq;
using HarmonyLib;
using SandBox.View.Map.Navigation;
using TaleWorlds.CampaignSystem;
using TAOM.Core.Logging;
using TAOM.Features.FiefManagement.UI;

namespace TAOM.Features.FiefManagement.Hooks;

/// <summary>
/// Adds the "Fiefs" button (#789) to the campaign map's navigation bar. <c>MapNavigationHandler</c>'s
/// constructor builds its element array from the protected virtual <c>OnCreateElements</c>; NavalDLC
/// overrides it and calls the base, so this postfix runs for both handlers and the button lands after
/// vanilla's seven (NavalDLC then inserts its own at index 3, which only shifts ours by one).
///
/// Runs inside the handler constructor: no handler state exists yet, so nothing here may read the
/// handler's element array or call anything that does. The element's collaborators are resolved here
/// and handed to its constructor. A throw anywhere in the build reaches the catch below and leaves
/// <c>__result</c> as the array vanilla built.
/// </summary>
[HarmonyPatch(typeof(MapNavigationHandler), "OnCreateElements")]
[HarmonyPatchCategory("Patch36_FiefManagement")]
public static class Patch36_MapNavigationElements
{
    private static IModLogger _logger;
    private static bool _exceptionLogged;

    [HarmonyPostfix]
    public static void Postfix(MapNavigationHandler __instance, ref INavigationElement[] __result)
    {
        try
        {
            __result = AppendOnce(__result, () => new TaomFiefsNavigationElement(
                __instance, IoC.Resolve<FiefHubOpener>(), IoC.Resolve<IModLogger>()));
        }
        catch (Exception ex)
        {
            if (_exceptionLogged) return;
            _exceptionLogged = true;
            try { (_logger ??= IoC.Resolve<IModLogger>())?.LogError($"[FiefManagement] Patch36_MapNavigationElements.Postfix threw: {ex}"); }
            catch { /* never throw out of a handler constructor */ }
        }
    }

    // Pure so it is testable without a Campaign. Returns the input unchanged when it is null, when it
    // already holds our element (a second pass over the same array) or when the factory returns null.
    // A throwing factory is the caller's catch to handle: the assignment never happens, so the vanilla
    // array stays as it was.
    internal static INavigationElement[] AppendOnce(INavigationElement[] elements, Func<INavigationElement> factory)
    {
        if (elements == null) return elements;
        if (elements.Any(e => e != null && e.StringId == TaomFiefsNavigationElement.ElementId)) return elements;

        var ours = factory();
        if (ours == null) return elements;

        var result = new INavigationElement[elements.Length + 1];
        Array.Copy(elements, result, elements.Length);
        result[elements.Length] = ours;
        return result;
    }
}
