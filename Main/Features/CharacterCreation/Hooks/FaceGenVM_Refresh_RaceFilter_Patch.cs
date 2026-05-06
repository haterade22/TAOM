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

    private static IModLogger ResolveLogger()
    {
        if (_logger != null) return _logger;
        try { _logger = IoC.Resolve<IModLogger>(); } catch { /* IoC not ready */ }
        return _logger;
    }
}
