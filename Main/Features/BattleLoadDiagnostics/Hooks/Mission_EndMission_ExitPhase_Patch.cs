using System.Runtime.CompilerServices;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.BattleLoadDiagnostics.Hooks;

// Exit phase 1 (issue #331) — Mission.EndMission is the single marker the engine sets when a
// mission decides to end (state -> EndingNextFrame); the actual teardown runs on the next
// frame. Opens the exit window and stamps T0 with mission/scene/agent-count + GC stats so
// the later phase deltas localize the tournament-exit hang.
[HarmonyPatch(typeof(Mission), nameof(Mission.EndMission))]
[HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
public static class Mission_EndMission_ExitPhase_Patch
{
    private static IBattleLoadDiagnosticsService? _service;

    // EndMission can be re-invoked for the same mission while it winds down; re-stamping
    // would restart the exit stopwatch mid-exit and corrupt the deltas. Track the last
    // stamped instance by identity hash (an int — never a strong ref that would keep the
    // torn-down Mission graph alive).
    private static int _lastStampedMissionHash;

    public static void Initialize(IBattleLoadDiagnosticsService service) => _service = service;

    [HarmonyPostfix]
    public static void Postfix(Mission __instance)
    {
        var svc = _service;
        if (svc == null || !svc.IsEnabled) return;
        try
        {
            // Campaign-scoped by design: the exit lifecycle ends at MapState activation,
            // which custom battles don't have — opening the window there would leak it
            // until the next campaign map activate and stamp a spurious MapResumed pair
            // (deep-review data-flow finding, 2026-07-06). Same guard precedent as
            // MissionState_OpenNew_Patch.
            if (TaleWorlds.CampaignSystem.Campaign.Current == null) return;

            int hash = RuntimeHelpers.GetHashCode(__instance);
            if (svc.IsExitWindowActive && hash == _lastStampedMissionHash) return;
            _lastStampedMissionHash = hash;

            string missionName = MissionState.Current?.MissionName ?? "<unknown>";
            string sceneName = __instance.SceneName ?? "<unknown>";
            int agents = __instance.Agents?.Count ?? -1;
            int allAgents = __instance.AllAgents?.Count ?? -1;
            svc.LogExitBegin(missionName, sceneName, agents, allAgents);
        }
        catch { /* diagnostic only */ }
    }
}
