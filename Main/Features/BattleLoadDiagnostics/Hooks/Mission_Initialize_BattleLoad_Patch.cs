using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.BattleLoadDiagnostics.Hooks;

// Phase 4 — Mission.Initialize entered. Opens the loading window (which gates phase-5
// per-agent logging and arms the stall watchdog) and writes the marker. A second prefix
// on Mission.Initialize coexists fine with Patch16_AtmospherePersistence.
[HarmonyPatch(typeof(Mission), "Initialize")]
[HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
public static class Mission_Initialize_BattleLoad_Patch
{
    private static IBattleLoadDiagnosticsService? _service;
    private static IBattleLoadStallMarker? _stallMarker;

    public static void Initialize(IBattleLoadDiagnosticsService service, IBattleLoadStallMarker stallMarker)
    {
        _service = service;
        _stallMarker = stallMarker;
    }

    [HarmonyPrefix]
    public static void Prefix(Mission __instance)
    {
        var svc = _service;
        if (svc == null || !svc.IsEnabled) return;

        // Open the window only when enabled — the watchdog and phase-5 are both gated on it,
        // and phase 6 closes it on the first playable tick.
        BattleLoadLoadingWindow.Enter();
        var scene = __instance?.SceneName ?? "<null>";
        // Write the inflight marker: if this load hangs and the player force-quits, the
        // surviving marker triggers a "send your log" notice on the next main menu.
        try { _stallMarker?.MarkInflight(scene); } catch { /* diagnostic only */ }
        try { svc.LogMissionInitialize(scene); }
        catch { /* diagnostic only */ }
    }
}
