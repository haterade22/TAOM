using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.BattleLoadDiagnostics.Hooks;

// Phases 4d/4e — brackets MissionState.FinishMissionLoading, which is what finally runs once the
// native Mission.IsLoadingFinished poll returns true (MissionState.cs:229-231, 333-350).
//
// This closes the second and third buckets of the 11.9-second MissionInitialize ->
// MissionAfterStartBegin gap measured on 2026-08-07:
//   MissionInitializeDone   -> FinishMissionLoadingBegin : the async wait (carries polls=/waitMs=)
//   FinishMissionLoadingBegin -> MissionAfterStartBegin  : Scene.SetOwnerThread + two warm-up
//                                                          Mission.Tick(0.001f) calls +
//                                                          Handler.OnMissionAfterStarting
//   MissionAfterStartDone   -> FinishMissionLoadingDone  : OnMissionLoadingFinished +
//                                                          Scene.ResumeLoadingRenderings
// With these there is no unattributed span left between MissionInitialize and BattlePlayable.
//
// FinishMissionLoading is PRIVATE, parameterless and non-overloaded (MissionState.cs:333), bound by
// string exactly like the sibling MissionState.OnFinalize and MissionState.LoadMission patches in
// this category. AccessTools.Method searches non-public, so HarmonyPatchBindingTests resolves it
// without extra wiring.
[HarmonyPatch(typeof(MissionState), "FinishMissionLoading")]
[HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
public static class MissionState_FinishMissionLoading_BattleLoad_Patch
{
    private static IBattleLoadDiagnosticsService? _service;

    public static void Initialize(IBattleLoadDiagnosticsService service) => _service = service;

    [HarmonyPrefix]
    public static void Prefix()
    {
        try { _service?.LogFinishMissionLoadingBegin(); }
        catch { /* diagnostic only — a diagnostic must never break a mission load */ }
    }

    [HarmonyPostfix]
    public static void Postfix()
    {
        try { _service?.LogFinishMissionLoadingDone(); }
        catch { /* diagnostic only */ }
    }
}
