using HarmonyLib;
using SandBox.View.Map;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.FiefManagement.Hooks;

/// <summary>
/// F6 on the campaign map. The guards, the "no fiefs" message and the menu activation live in
/// <see cref="FiefHubOpener"/> and the map-menu gate behind it; F6 and the navigation-bar button share both.
/// </summary>
[HarmonyPatch(typeof(MapScreen), "OnFrameTick")]
[HarmonyPatchCategory("Patch36_FiefManagement")]
public static class Patch36_MapScreenF6
{
    private static IMapScreenInputAdapter _input;
    private static FiefHubOpener _opener;
    private static IModLogger _logger;
    private static bool _exceptionLogged;

    [HarmonyPostfix]
    public static void Postfix()
    {
        try
        {
            var input = _input ??= IoC.Resolve<IMapScreenInputAdapter>();
            if (!input.IsF6Pressed) return;

            (_opener ??= IoC.Resolve<FiefHubOpener>()).TryOpen("F6");
        }
        catch (System.Exception ex)
        {
            // F6 polling fires every frame: never throw out of the postfix. Log once per process
            // so a recurring exception is visible without spamming the log.
            if (!_exceptionLogged)
            {
                _exceptionLogged = true;
                try
                {
                    (_logger ??= IoC.Resolve<IModLogger>())?.LogError($"[FiefManagement] Patch36_MapScreenF6.Postfix threw: {ex}");
                }
                catch
                {
                    // If even the logger resolution fails, swallow: never throw from a per-frame postfix.
                }
            }
        }
    }
}
