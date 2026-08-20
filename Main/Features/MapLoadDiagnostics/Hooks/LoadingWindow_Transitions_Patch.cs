using HarmonyLib;
using TaleWorlds.Engine;

namespace TAOM.Features.MapLoadDiagnostics.Hooks;

/// <summary>
/// Traces every raise and lower of the global loading window, WITH the managed caller chain.
///
/// <para>
/// This is the central question of the v1.5.0 map-load stall. The heartbeat proved the map runs at
/// 85 fps with a 5 ms campaign tick and nothing spawning, while <c>loadingWindow</c> stays true
/// indefinitely. TAOM makes no calls to either method, so whatever raises it is vanilla, and the
/// interesting fact is which vanilla path did so and whether its matching lower ever runs.
/// </para>
///
/// <para>
/// Caller chains are affordable here because these fire a handful of times per session, not per
/// frame.
/// </para>
/// </summary>
[HarmonyPatch(typeof(LoadingWindow), nameof(LoadingWindow.EnableGlobalLoadingWindow))]
[HarmonyPatchCategory("Patch66_MapLoadDiagnostics_Lifecycle")]
public static class LoadingWindow_Enable_Patch
{
    [HarmonyPostfix]
    public static void Postfix() => MapLoadTracer.TraceWithCallers("LOADING-WINDOW raised");
}

/// <summary>Counterpart to <see cref="LoadingWindow_Enable_Patch"/>; its absence is the symptom.</summary>
[HarmonyPatch(typeof(LoadingWindow), nameof(LoadingWindow.DisableGlobalLoadingWindow))]
[HarmonyPatchCategory("Patch66_MapLoadDiagnostics_Lifecycle")]
public static class LoadingWindow_Disable_Patch
{
    [HarmonyPostfix]
    public static void Postfix() => MapLoadTracer.TraceWithCallers("LOADING-WINDOW lowered");
}
