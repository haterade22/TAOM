using System.Linq;
using HarmonyLib;
using TaleWorlds.Core;

namespace TAOM.Features.MapLoadDiagnostics.Hooks;

/// <summary>
/// Traces every game-state push, pop and clean, recording the resulting stack each time.
///
/// <para>
/// A state left pushed above <c>MapState</c> would hold the loading overlay while the map ticks
/// underneath, which is exactly the shape the heartbeat found. The active state alone cannot show
/// that (it would read <c>MapState</c> and look healthy), so each event records the whole stack
/// bottom to top. Read as a timeline, an unmatched push is visible at a glance.
/// </para>
/// </summary>
public static class GameStateTraceHelper
{
    public static string Stack(GameStateManager gsm)
    {
        try
        {
            if (gsm == null) return "<null manager>";
            var names = gsm.GameStates.Select(s => s?.GetType().Name ?? "<null>").ToArray();
            return names.Length == 0 ? "<empty>" : string.Join(" > ", names);
        }
        catch { return "<unreadable>"; }
    }
}

[HarmonyPatch(typeof(GameStateManager), nameof(GameStateManager.PushState))]
[HarmonyPatchCategory("Patch66_MapLoadDiagnostics_Lifecycle")]
public static class GameStateManager_PushState_Patch
{
    [HarmonyPostfix]
    public static void Postfix(GameStateManager __instance, GameState gameState, int level)
        => MapLoadTracer.Trace($"STATE push {gameState?.GetType().Name ?? "<null>"} (level {level})",
                               "stack: " + GameStateTraceHelper.Stack(__instance));
}

[HarmonyPatch(typeof(GameStateManager), nameof(GameStateManager.PopState))]
[HarmonyPatchCategory("Patch66_MapLoadDiagnostics_Lifecycle")]
public static class GameStateManager_PopState_Patch
{
    [HarmonyPostfix]
    public static void Postfix(GameStateManager __instance, int level)
        => MapLoadTracer.Trace($"STATE pop (level {level})",
                               "stack: " + GameStateTraceHelper.Stack(__instance));
}

[HarmonyPatch(typeof(GameStateManager), nameof(GameStateManager.CleanAndPushState))]
[HarmonyPatchCategory("Patch66_MapLoadDiagnostics_Lifecycle")]
public static class GameStateManager_CleanAndPushState_Patch
{
    [HarmonyPostfix]
    public static void Postfix(GameStateManager __instance, GameState gameState, int level)
        => MapLoadTracer.Trace($"STATE cleanAndPush {gameState?.GetType().Name ?? "<null>"} (level {level})",
                               "stack: " + GameStateTraceHelper.Stack(__instance));
}

[HarmonyPatch(typeof(GameStateManager), nameof(GameStateManager.CleanStates))]
[HarmonyPatchCategory("Patch66_MapLoadDiagnostics_Lifecycle")]
public static class GameStateManager_CleanStates_Patch
{
    [HarmonyPostfix]
    public static void Postfix(GameStateManager __instance, int level)
        => MapLoadTracer.Trace($"STATE cleanStates (level {level})",
                               "stack: " + GameStateTraceHelper.Stack(__instance));
}
