using HarmonyLib;
using TaleWorlds.Engine;

namespace TAOM.Features.BattleLoadDiagnostics.Hooks;

// Phase 2d — the highest-value stamp in the battle-load window. ClearOldResourcesAndObjects is
// the ONE native call between LoadMission and Mission.Initialize (MissionState.LoadMission calls
// it immediately before Initialize), it walks the whole resource table, and it is exactly the
// shape that access-violates when a previous mission left the native heap corrupt. A Begin with
// no Done puts the fault in native resource teardown instead of managed load code — a distinction
// the log could not previously make.
//
// It has exactly one caller in the shipping build (MissionState.LoadMission), so this probe is
// scoped to the load path and adds no noise elsewhere.
[HarmonyPatch(typeof(Utilities), nameof(Utilities.ClearOldResourcesAndObjects))]
[HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
public static class Utilities_ClearOldResourcesAndObjects_BattleLoad_Patch
{
    private static IBattleLoadDiagnosticsService? _service;

    public static void Initialize(IBattleLoadDiagnosticsService service) => _service = service;

    [HarmonyPrefix]
    public static void Prefix()
    {
        var svc = _service;
        if (svc == null || !svc.IsEnabled) return;
        try { svc.LogResourceClearOldBegin(); }
        catch { /* diagnostic only */ }
    }

    [HarmonyPostfix]
    public static void Postfix()
    {
        var svc = _service;
        if (svc == null || !svc.IsEnabled) return;
        try { svc.LogResourceClearOldDone(); }
        catch { /* diagnostic only */ }
    }
}
