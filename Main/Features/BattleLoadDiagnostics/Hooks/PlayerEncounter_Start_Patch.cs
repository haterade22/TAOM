using HarmonyLib;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Features.BattleLoadDiagnostics.Hooks;

// Phase 1 — encounter initiated on the world map. Resets the lifecycle clock and writes
// the first marker. Rich attacker/defender data isn't populated at Start() yet, so the
// detailed vs-line is emitted at Phase 2 (MissionState.OpenNew) instead.
[HarmonyPatch(typeof(PlayerEncounter), nameof(PlayerEncounter.Start))]
[HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
public static class PlayerEncounter_Start_Patch
{
    private static IBattleLoadDiagnosticsService? _service;

    public static void Initialize(IBattleLoadDiagnosticsService service) => _service = service;

    [HarmonyPostfix]
    public static void Postfix()
    {
        var svc = _service;
        if (svc == null || !svc.IsEnabled) return;
        try
        {
            svc.ResetLifecycle();
            int size = MobileParty.MainParty?.MemberRoster?.TotalManCount ?? 0;
            svc.LogEncounterStart(size);
        }
        catch { /* diagnostic only — never break encounter start */ }
    }
}
