using HarmonyLib;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.BattleLoadDiagnostics.Hooks;

// Phase 2 — MissionState.OpenNew is the single funnel for every mission. Logs the chosen
// scene name + the rich encounter summary (attacker/defender, sizes, player side), which
// is reliably populated by mission-open time.
[HarmonyPatch(typeof(MissionState), nameof(MissionState.OpenNew))]
[HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
public static class MissionState_OpenNew_Patch
{
    private static IBattleLoadDiagnosticsService? _service;

    public static void Initialize(IBattleLoadDiagnosticsService service) => _service = service;

    [HarmonyPrefix]
    public static void Prefix(string missionName, MissionInitializerRecord rec)
    {
        var svc = _service;
        if (svc == null || !svc.IsEnabled) return;
        try
        {
            svc.LogMissionOpenNew(missionName ?? "<null>", rec.SceneName ?? "<null>", BuildEncounterSummary());
        }
        catch { /* diagnostic only */ }
    }

    private static string? BuildEncounterSummary()
    {
        try
        {
            var enc = PlayerEncounter.Current;
            if (enc == null) return null;

            string encountered = PlayerEncounter.EncounteredParty?.Name?.ToString() ?? "<none>";
            int main = MobileParty.MainParty?.MemberRoster?.TotalManCount ?? 0;
            int enemy = PlayerEncounter.EncounteredMobileParty?.MemberRoster?.TotalManCount ?? 0;
            return $"encountered='{encountered}' side={enc.PlayerSide} main={main} enemy={enemy}";
        }
        catch { return null; }
    }
}
