using HarmonyLib;
using TaleWorlds.Engine;

namespace TAOM.Features.MapLoadDiagnostics.Hooks;

/// <summary>
/// Traces every raise, and every lower that actually took the window down, WITH the managed caller
/// chain.
///
/// <para>
/// This is the central question of the v1.5.0 map-load stall. The heartbeat proved the map runs at
/// 85 fps with a 5 ms campaign tick and nothing spawning, while <c>loadingWindow</c> stays true
/// indefinitely. TAOM makes no calls to either method, so whatever raises it is vanilla, and the
/// interesting fact is which vanilla path did so and whether its matching lower ever runs.
/// </para>
///
/// <para>
/// Caller chains are affordable only on real transitions. Raises are rare, but the engine calls
/// <c>DisableGlobalLoadingWindow</c> on every frame of the main menu and several campaign screens
/// (party, inventory, clan, kingdom, quests, character and crafting among them), and of scene
/// screens such as character creation, the barber, the face generator and the banner editor once
/// their scene is ready, and clears the flag whether or not the window was up.
/// The Disable patch therefore captures the flag in a Prefix and traces only a true-to-false change
/// (otherwise one no-op lower per rendered frame, each a stack walk and a flushed log line).
/// </para>
/// </summary>
[HarmonyPatch(typeof(LoadingWindow), nameof(LoadingWindow.EnableGlobalLoadingWindow))]
[HarmonyPatchCategory("Patch89_MapLoadDiagnostics_Lifecycle")]
public static class LoadingWindow_Enable_Patch
{
    [HarmonyPostfix]
    public static void Postfix() => MapLoadTracer.TraceWithCallers("LOADING-WINDOW raised");
}

/// <summary>Counterpart to <see cref="LoadingWindow_Enable_Patch"/>; its absence is the symptom.</summary>
[HarmonyPatch(typeof(LoadingWindow), nameof(LoadingWindow.DisableGlobalLoadingWindow))]
[HarmonyPatchCategory("Patch89_MapLoadDiagnostics_Lifecycle")]
public static class LoadingWindow_Disable_Patch
{
    [HarmonyPrefix]
    public static void Prefix(out bool __state) => __state = LoadingWindow.IsLoadingWindowActive;

    // TraceWithCallers skips two frames (itself and this Postfix), so it must be called from here
    // directly, never through a helper.
    [HarmonyPostfix]
    public static void Postfix(bool __state)
    {
        if (LoadingWindowTraceGate.IsRealLower(__state, LoadingWindow.IsLoadingWindowActive))
            MapLoadTracer.TraceWithCallers("LOADING-WINDOW lowered");
    }
}
