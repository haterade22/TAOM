using HarmonyLib;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Map;

namespace TAOM.Features.BattleLoadDiagnostics.Hooks;

// Phase 3 — the battle-terrain scene chosen for the world-map tile. Postfix on the
// vanilla scene model (no TAOM scene-model override exists). A weird/empty sceneId here,
// or a Debug.FailedAssert fallback in the engine, localizes a scene-side hang vs an
// equipment-side hang.
[HarmonyPatch(typeof(DefaultSceneModel), nameof(DefaultSceneModel.GetBattleSceneForMapPatch))]
[HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
public static class BattleSceneSelection_Patch
{
    private static IBattleLoadDiagnosticsService? _service;

    public static void Initialize(IBattleLoadDiagnosticsService service) => _service = service;

    [HarmonyPostfix]
    public static void Postfix(string __result, MapPatchData mapPatch, bool isNavalEncounter)
    {
        var svc = _service;
        if (svc == null || !svc.IsEnabled) return;
        try
        {
            svc.LogBattleSceneSelected(mapPatch.sceneIndex, __result ?? "<null>", isNavalEncounter);
        }
        catch { /* diagnostic only */ }
    }
}
