using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.BattleLoadDiagnostics.Hooks;

// Exit phase 2 (issue #331) — the private Mission.EndMissionInternal runs the managed
// teardown: every behavior's OnEndMission/OnEndMissionInternal, agent OnRemove/OnDelete,
// mission-object OnEndMission, then FreeResources + the native FinalizeMission call. The
// Begin->Done delta is the whole managed-teardown + native-finalize window.
[HarmonyPatch(typeof(Mission), "EndMissionInternal")]
[HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
public static class Mission_EndMissionInternal_ExitPhase_Patch
{
    private static IBattleLoadDiagnosticsService? _service;

    public static void Initialize(IBattleLoadDiagnosticsService service) => _service = service;

    [HarmonyPrefix]
    public static void Prefix()
    {
        try { _service?.LogExitTeardownBegin(); }
        catch { /* diagnostic only */ }
    }

    [HarmonyPostfix]
    public static void Postfix()
    {
        try { _service?.LogExitTeardownDone(); }
        catch { /* diagnostic only */ }
    }
}
