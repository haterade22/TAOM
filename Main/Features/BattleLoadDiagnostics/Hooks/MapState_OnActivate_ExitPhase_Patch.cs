using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;

namespace TAOM.Features.BattleLoadDiagnostics.Hooks;

// Exit phase 6 (issue #331) — MapState.OnActivate fires when the campaign map becomes the
// active state again after the MissionState pops: the exit loading screen is effectively
// over. Stamps GC stats (delta vs ExitBegin exposes the mission-end full GC) and whether a
// save is running (SaveHandler.IsSaving — a save inside the exit window means the hang is
// save-time, not teardown). Also fires at campaign start/load; the service's exit-window
// gate keeps those silent.
[HarmonyPatch(typeof(MapState), "OnActivate")]
[HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
public static class MapState_OnActivate_ExitPhase_Patch
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
            svc.LogMapResumed(isSaving);
        }
        catch { /* diagnostic only */ }
    }
}
