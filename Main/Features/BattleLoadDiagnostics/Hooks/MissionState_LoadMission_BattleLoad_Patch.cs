using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.BattleLoadDiagnostics.Hooks;

// Phase 2c — MissionState.LoadMission (private, parameterless) runs on the tick AFTER OpenNew
// returns, and is the last managed staging post before the mission exists: it walks every
// behavior's OnMissionScreenPreLoad, calls the native Utilities.ClearOldResourcesAndObjects(),
// then Mission.Initialize(). Stamping its entry splits the old MissionOpenNew->MissionInitialize
// dark window at the tick boundary, so a report tells us which side of the boundary died.
//
// String binding to a private method is deliberate and has precedent in this same category (see
// Mission_EndMissionInternal_ExitPhase_Patch) — Harmony resolves it via AccessTools, and
// LoadMission is parameterless and non-overloaded, so there is no ambiguity.
[HarmonyPatch(typeof(MissionState), "LoadMission")]
[HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
public static class MissionState_LoadMission_BattleLoad_Patch
{
    private static IBattleLoadDiagnosticsService? _service;

    public static void Initialize(IBattleLoadDiagnosticsService service) => _service = service;

    [HarmonyPrefix]
    public static void Prefix()
    {
        var svc = _service;
        if (svc == null || !svc.IsEnabled) return;
        try { svc.LogLoadMissionBegin(); }
        catch { /* diagnostic only */ }
    }
}
