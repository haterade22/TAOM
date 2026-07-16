using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.BattleLoadDiagnostics.Hooks;

// Phase 4b — Mission.AfterStart is where OnMissionBehaviorInitialize runs for EVERY loaded
// submodule (Mission.AfterStart -> CollectSubModules -> per-submodule OnMissionBehaviorInitialize),
// reached from MissionState.FinishMissionLoading well AFTER Mission.Initialize returns.
//
// This bracket is what makes TAOM's own behavior stamps exonerating rather than merely
// incriminating: the gap between MissionAfterStartBegin and TaomBehaviorsBegin is other mods'
// behavior construction. Without it, "died after Initialize, before BattlePlayable" could never be
// pinned on anyone.
[HarmonyPatch(typeof(Mission), nameof(Mission.AfterStart))]
[HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
public static class Mission_AfterStart_BattleLoad_Patch
{
    private static IBattleLoadDiagnosticsService? _service;

    public static void Initialize(IBattleLoadDiagnosticsService service) => _service = service;

    [HarmonyPrefix]
    public static void Prefix()
    {
        var svc = _service;
        if (svc == null || !svc.IsEnabled) return;
        try { svc.LogMissionAfterStartBegin(); }
        catch { /* diagnostic only */ }
    }

    [HarmonyPostfix]
    public static void Postfix()
    {
        var svc = _service;
        if (svc == null || !svc.IsEnabled) return;
        try { svc.LogMissionAfterStartDone(); }
        catch { /* diagnostic only */ }
    }
}
