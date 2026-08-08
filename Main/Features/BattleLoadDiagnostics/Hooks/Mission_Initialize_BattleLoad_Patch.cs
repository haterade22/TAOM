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
        if (svc == null) return;

        var scene = __instance?.SceneName ?? "<null>";
        if (svc.IsEnabled)
        {
            // Open the window only when enabled — the watchdog and phase-5 are both gated
            // on it, and phase 6 closes it on the first playable tick.
            BattleLoadLoadingWindow.Enter();
            // Write the inflight marker: if this load hangs and the player force-quits, the
            // surviving marker triggers a "send your log" notice on the next main menu.
            try { _stallMarker?.MarkInflight(scene); } catch { /* diagnostic only */ }
        }

        // Called even while the master toggle is off: LogMissionInitialize's stale-exit-window
        // close is an unconditional state transition (the phase logging inside self-gates on
        // IsEnabled). A hook-level IsEnabled gate here bypassed the closer — a toggle-off
        // mid-exit-window could leave the window latched (Codex review 2026-07-06, P2).
        try { svc.LogMissionInitialize(scene); }
        catch { /* diagnostic only */ }
    }

    // Phase 4a — Mission.Initialize returned. The prefix above and this postfix bracket the single
    // native MBAPI.IMBMission.InitializeMission call that is Initialize's entire body
    // (Mission.cs:1798-1809), which is the first of the three buckets the 11.9-second
    // MissionInitialize -> MissionAfterStartBegin gap decomposes into.
    //
    // Unconditional, like the prefix's LogMissionInitialize call: this zeroes the loading-poll
    // counter and arms the wait clock (state transitions), and self-gates its own logging on
    // IsEnabled. A hook-level gate here would let a mid-load toggle-off strand a stale counter.
    [HarmonyPostfix]
    public static void Postfix(Mission __instance)
    {
        var svc = _service;
        if (svc == null) return;

        try { svc.LogMissionInitializeDone(__instance?.SceneName ?? "<null>"); }
        catch { /* diagnostic only */ }
    }
}
