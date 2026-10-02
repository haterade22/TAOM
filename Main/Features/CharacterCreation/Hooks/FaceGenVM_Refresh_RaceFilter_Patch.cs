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

        // #704. Likewise while a hero picked on the themed faction screen is applied: his race was
        // copied onto the player before the face generator opened, and the face generator must show
        // it, not the culture's first race.
        if (ResolveFactionPresets()?.HasPick == true) return;

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

    private static TAOM.Features.FactionUI.Presets.FactionPresetService _factionPresets;

    private static TAOM.Features.FactionUI.Presets.FactionPresetService ResolveFactionPresets()
    {
        if (_factionPresets != null) return _factionPresets;
        try { _factionPresets = IoC.Resolve<TAOM.Features.FactionUI.Presets.FactionPresetService>(); }
        catch { /* IoC not ready */ }
        return _factionPresets;
    }

    private static IModLogger ResolveLogger()
    {
        if (_logger != null) return _logger;
        try { _logger = IoC.Resolve<IModLogger>(); } catch { /* IoC not ready */ }
        return _logger;
    }
}
