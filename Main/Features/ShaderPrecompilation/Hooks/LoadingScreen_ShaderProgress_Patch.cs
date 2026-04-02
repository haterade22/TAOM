using System;
using System.Reflection;
using HarmonyLib;
using TAOM.Core.Logging;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade.GauntletUI;

namespace TAOM.Features.ShaderPrecompilation.Hooks;

// Patches LoadingWindowViewModel.Update() to show shader compilation progress.
// Update() is internal (confirmed via decompilation) — AccessTools.Method() bypasses the access modifier.
// DescriptionText has a public setter and is MVVM data-bound — safe to write from any thread.
// The text is only shown during loading screens; it clears when compilation reaches 0.
[HarmonyPatch]
[HarmonyPatchCategory("Patch21_ShaderPrecompilation")]
public static class LoadingScreen_ShaderProgress_Patch
{
    private static IModLogger _logger;

    public static void Initialize(IModLogger logger)
    {
        _logger = logger;
    }

    static MethodBase TargetMethod()
        => AccessTools.Method(typeof(LoadingWindowViewModel), "Update");

    [HarmonyPostfix]
    public static void Postfix(LoadingWindowViewModel __instance)
    {
        try
        {
            if (!__instance.Enabled) return;

            int remaining = Utilities.GetNumberOfShaderCompilationsInProgress();
            if (remaining > 0)
            {
                __instance.DescriptionText = $"Compiling shaders... {remaining} remaining";
            }
            else if (__instance.DescriptionText?.StartsWith("Compiling shaders") == true)
            {
                __instance.DescriptionText = string.Empty;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[ShaderPrecompilation] LoadingScreen patch error: {ex.Message}");
        }
    }
}
