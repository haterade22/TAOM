using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;

namespace TAOM.Features.BattleLoadDiagnostics.Hooks;

// Exit phase 7 (issue #331) — the first MapState.OnTick after MapResumed closes the exit
// window: the MapResumed->FirstMapTick delta captures menu/VM re-init cost after activation.
// HOT PATH: this postfix runs on every map frame forever, so the inactive-window early-out
// is the first statement and does no work beyond two reads (per harmony-patches.md).
[HarmonyPatch(typeof(MapState), "OnTick")]
[HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
public static class MapState_OnTick_ExitPhase_Patch
{
    private static IBattleLoadDiagnosticsService? _service;

    public static void Initialize(IBattleLoadDiagnosticsService service) => _service = service;

    [HarmonyPostfix]
    public static void Postfix()
    {
        var svc = _service;
        if (svc == null || !svc.IsExitWindowActive) return;
        try
        {
            bool isSaving = Campaign.Current?.SaveHandler?.IsSaving ?? false;
            svc.LogFirstMapTick(isSaving); // closes the exit window
        }
        catch { /* diagnostic only */ }
    }
}
