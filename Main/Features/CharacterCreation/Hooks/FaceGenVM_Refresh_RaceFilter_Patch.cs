using System;
using HarmonyLib;
using TaleWorlds.MountAndBlade.ViewModelCollection.FaceGenerator;
using TAOM.Core.Logging;

namespace TAOM.Features.CharacterCreation.Hooks;

[HarmonyPatch(typeof(FaceGenVM), "Refresh", new[] { typeof(bool) })]
[HarmonyPatchCategory("Patch9_RaceFilter")]
public static class FaceGenVM_Refresh_RaceFilter_Patch
{
    private static ICultureRaceFilterService _filterService;
    private static IModLogger _logger;

    [HarmonyPostfix]
    public static void Postfix(FaceGenVM __instance, bool clearProperties)
    {
        if (!clearProperties) return;

        // #514. While the Player Switcher is previewing a chosen lord, leave the race dropdown
        // alone. SetBodyProperties triggers Refresh(clearProperties: true) on every race change,
        // so rebuilding the selector down to the culture's allowed races here would visibly snap a
        // dwarf or a Sauron preview back to the culture default the instant it was applied.
        if (ResolvePlayerSwitchSession()?.IsPreviewActive == true) return;

        try
        {
            FaceGenRaceSelectorRebuilder.Apply(__instance, ResolveFilterService());
        }
        catch (Exception ex)
        {
            ResolveLogger()?.LogError($"FaceGenVM_Refresh_RaceFilter_Patch: {ex.GetType().Name} {ex.Message}");
        }
    }

    private static ICultureRaceFilterService ResolveFilterService()
    {
        if (_filterService != null) return _filterService;
        try { _filterService = IoC.Resolve<ICultureRaceFilterService>(); } catch { /* IoC not ready */ }
        return _filterService;
    }

    private static TAOM.Features.PlayerSwitcher.IPlayerSwitchSession _playerSwitchSession;

    private static TAOM.Features.PlayerSwitcher.IPlayerSwitchSession ResolvePlayerSwitchSession()
    {
        if (_playerSwitchSession != null) return _playerSwitchSession;
        try { _playerSwitchSession = IoC.Resolve<TAOM.Features.PlayerSwitcher.IPlayerSwitchSession>(); }
        catch { /* IoC not ready */ }
        return _playerSwitchSession;
    }

    private static IModLogger ResolveLogger()
    {
        if (_logger != null) return _logger;
        try { _logger = IoC.Resolve<IModLogger>(); } catch { /* IoC not ready */ }
        return _logger;
    }
}
