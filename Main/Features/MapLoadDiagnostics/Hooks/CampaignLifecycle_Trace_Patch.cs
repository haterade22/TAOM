using HarmonyLib;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;

namespace TAOM.Features.MapLoadDiagnostics.Hooks;

/// <summary>
/// Traces the campaign and map-screen lifecycle seams around a new-game start.
///
/// <para>
/// The heartbeat narrowed the v1.5.0 stall to a completion signal that never fires: the map runs,
/// the overlay stays. These are the seams that normally mark "the campaign is ready" and "the map
/// screen is ready", so the last one that fires bounds where the sequence stopped, and the ones
/// that never fire name what is missing.
/// </para>
///
/// <para>
/// Separate category from the heartbeat: Harmony aborts a category at the first failing class, and
/// these bind engine types that could drift. A drift here must not take the working heartbeat with
/// it (the Patch61 precedent).
/// </para>
/// </summary>
[HarmonyPatch(typeof(MapState), "OnActivate")]
[HarmonyPatchCategory("Patch66_MapLoadDiagnostics_Lifecycle")]
public static class MapState_OnActivate_Trace_Patch
{
    [HarmonyPrefix] public static void Prefix() => MapLoadTracer.Trace("MapState.OnActivate ENTER");
    [HarmonyPostfix] public static void Postfix() => MapLoadTracer.Trace("MapState.OnActivate EXIT");
}

/// <summary>
/// NOTE the resolved target: <c>MapState</c> does not override <c>OnInitialize</c>, so this binds
/// the BASE <c>GameState.OnInitialize</c> and therefore fires for every game state, not just the
/// map. That is more useful than the narrower binding would have been, but only if the line names
/// the real instance type instead of asserting "MapState" for all of them. The snapshot caught the
/// discrepancy; the label follows the binding.
/// </summary>
[HarmonyPatch(typeof(GameState), "OnInitialize")]
[HarmonyPatchCategory("Patch66_MapLoadDiagnostics_Lifecycle")]
public static class GameState_OnInitialize_Trace_Patch
{
    [HarmonyPostfix]
    public static void Postfix(GameState __instance)
        => MapLoadTracer.Trace($"STATE initialized: {__instance?.GetType().Name ?? "<null>"}");
}

/// <summary>
/// The map SCREEN, as distinct from the map STATE. The engine log already showed
/// <c>TopScreen: MapScreen</c> while the overlay was up, so bracketing its initialize says whether
/// the screen finished coming up or is still inside its own construction.
/// </summary>
[HarmonyPatch(typeof(MapScreen), "OnInitialize")]
[HarmonyPatchCategory("Patch66_MapLoadDiagnostics_MapScreen")]
public static class MapScreen_OnInitialize_Trace_Patch
{
    [HarmonyPrefix] public static void Prefix() => MapLoadTracer.Trace("MapScreen.OnInitialize ENTER");
    [HarmonyPostfix] public static void Postfix() => MapLoadTracer.Trace("MapScreen.OnInitialize EXIT");
}

[HarmonyPatch(typeof(MapScreen), "OnFrameTick")]
[HarmonyPatchCategory("Patch66_MapLoadDiagnostics_MapScreen")]
public static class MapScreen_FirstFrame_Trace_Patch
{
    private static bool _logged;

    // Only the FIRST frame matters: it proves the screen reached its render loop. Logging every
    // frame would flood the file and change the timing being measured.
    [HarmonyPostfix]
    public static void Postfix()
    {
        if (_logged) return;
        _logged = true;
        MapLoadTracer.Trace("MapScreen FIRST FrameTick completed");
    }
}
