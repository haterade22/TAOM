using System;
using System.Reflection;
using HarmonyLib;
using TAOM.Core.Logging;
using TaleWorlds.MountAndBlade.GauntletUI;

namespace TAOM.Features.ShaderPrecompilation.Hooks;

// Mirrors ShaderPrecompileRunner.StatusLine onto the loading-screen description text during the
// shader walk. The loading screen is up for most of each item's compile, so this is the primary
// visible surface. All compile-detection / latch logic now lives in the unit-tested
// ShaderPrecompileDecider + ShaderPrecompileRunner — this patch is a thin display mirror.
//
// LoadingWindowViewModel.Update() is internal (AccessTools bypasses it); DescriptionText has a
// public setter and is MVVM data-bound — safe to write from the UI thread.
[HarmonyPatch]
[HarmonyPatchCategory("Patch21_ShaderPrecompilation")]
public static class LoadingScreen_ShaderProgress_Patch
{
    private static IModLogger _logger;
    private static ShaderPrecompileRunner _runner;

    public static void Initialize(IModLogger logger, ShaderPrecompileRunner runner)
    {
        _logger = logger;
        _runner = runner;
    }

    static MethodBase TargetMethod()
        => AccessTools.Method(typeof(LoadingWindowViewModel), "Update");

    [HarmonyPostfix]
    public static void Postfix(LoadingWindowViewModel __instance)
    {
        try
        {
            if (__instance == null || !__instance.Enabled) return;
            var runner = _runner;
            if (runner == null || !runner.IsActive) return;

            var status = runner.StatusLine;
            if (!string.IsNullOrEmpty(status))
                __instance.DescriptionText = status;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[ShaderPrecompilation] loading-screen patch error: {ex.Message}");
        }
    }
}
