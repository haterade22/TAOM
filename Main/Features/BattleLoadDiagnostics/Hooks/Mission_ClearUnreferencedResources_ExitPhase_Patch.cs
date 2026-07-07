using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.BattleLoadDiagnostics.Hooks;

// Exit phase 5 (issue #331) — Mission.ClearUnreferencedResources runs Common.MemoryCleanupGC()
// (a forced full GC) and, when forceClearGPUResources, the native MBAPI ClearResources call.
// It fires at mission LOAD too; the service's exit-window gate keeps those calls silent, so
// only the exit-time invocation (from Mission.OnMissionStateFinalize) is stamped. The
// Begin->Done delta isolates "the hang is the mission-end GC / GPU resource clear".
[HarmonyPatch(typeof(Mission), nameof(Mission.ClearUnreferencedResources))]
[HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
public static class Mission_ClearUnreferencedResources_ExitPhase_Patch
{
    private static IBattleLoadDiagnosticsService? _service;

    public static void Initialize(IBattleLoadDiagnosticsService service) => _service = service;

    [HarmonyPrefix]
    public static void Prefix(bool forceClearGPUResources)
    {
        try { _service?.LogExitResourceClearBegin(forceClearGPUResources); }
        catch { /* diagnostic only */ }
    }

    [HarmonyPostfix]
    public static void Postfix()
    {
        try { _service?.LogExitResourceClearDone(); }
        catch { /* diagnostic only */ }
    }
}
