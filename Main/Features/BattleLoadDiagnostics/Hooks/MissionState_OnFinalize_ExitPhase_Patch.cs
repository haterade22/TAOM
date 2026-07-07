using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.BattleLoadDiagnostics.Hooks;

// Exit phases 3/4 (issue #331) — MissionState.OnFinalize wraps Mission.OnMissionStateFinalize:
// per-behavior OnMissionStateFinalized, the RemoveMissionBehavior loop (every OnRemoveBehavior),
// and ClearUnreferencedResources (forced GC + native GPU clear when NeedsMemoryCleanup). This
// fires when GameStateManager pops the MissionState — i.e. inside the exit loading screen.
[HarmonyPatch(typeof(MissionState), "OnFinalize")]
[HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
public static class MissionState_OnFinalize_ExitPhase_Patch
{
    private static IBattleLoadDiagnosticsService? _service;

    public static void Initialize(IBattleLoadDiagnosticsService service) => _service = service;

    [HarmonyPrefix]
    public static void Prefix()
    {
        try { _service?.LogExitStateFinalizeBegin(); }
        catch { /* diagnostic only */ }
    }

    [HarmonyPostfix]
    public static void Postfix()
    {
        try { _service?.LogExitStateFinalizeDone(); }
        catch { /* diagnostic only */ }
    }
}
