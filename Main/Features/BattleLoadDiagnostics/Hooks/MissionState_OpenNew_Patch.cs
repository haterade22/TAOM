using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
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

    // The Prefix above stamps ENTRY, so on its own it cannot distinguish "died inside OpenNew"
    // from "died later in the load". This Postfix closes that: no MissionOpenNewDone after a
    // MissionOpenNew means the fault is in OpenNew's body -- OnMissionIsStarting, the native
    // Mission ctor, the SandBox behavior handler, or PushState. Reads __result but never writes
    // it, so it cannot alter engine control flow.
    [HarmonyPostfix]
    public static void Postfix(string missionName, Mission __result)
    {
        var svc = _service;
        if (svc == null || !svc.IsEnabled) return;
        try
        {
            svc.LogMissionOpenNewDone(missionName ?? "<null>", __result != null);
        }
        catch { /* diagnostic only */ }
    }

    private static string? BuildEncounterSummary()
    {
        try
        {
            // PlayerEncounter.Current dereferences Campaign.Current, which is null in
            // Custom Battle / non-campaign game modes -- guard so the (caught) NRE stops
            // tripping break-on-thrown debugger sessions on every custom battle start.
            if (TaleWorlds.CampaignSystem.Campaign.Current == null) return null;

            var enc = PlayerEncounter.Current;
            if (enc == null) return null;

            string encountered = PlayerEncounter.EncounteredParty?.Name?.ToString() ?? "<none>";
            int main = MobileParty.MainParty?.MemberRoster?.TotalManCount ?? 0;
            int enemy = PlayerEncounter.EncounteredMobileParty?.MemberRoster?.TotalManCount ?? 0;
            var baseLine = $"encountered='{encountered}' side={enc.PlayerSide} main={main} enemy={enemy}";

            // [RaidSpawn][diag] TEMPORARY (#350 — empty deployment formations + Ready CTD when
            // attacking a raiding AI lord). Dumps the live MapEvent side composition so we can tell
            // whether the player's MainParty is actually on the attacker MapEventSide and what the
            // engine's per-side spawn budget (GetNumberOfInvolvedMen) resolves to. Remove after the
            // root cause is confirmed (per feedback_comprehensive_diag_logging_then_remove).
            //
            // KEEP: this earned its place on 2026-07-16 — it produced the only usable evidence in
            // the Nan Angren player CTD (5 attacker parties / 307 men vs 1 party / 63 in the battle
            // that succeeded 6 minutes earlier). Root cause is still unproven; do not delete until
            // #350 closes.
            var sideDump = BuildMapEventSideDump();
            return sideDump == null ? baseLine : baseLine + " | " + sideDump;
        }
        catch { return null; }
    }

    private static string? BuildMapEventSideDump()
    {
        try
        {
            var mapEvent = MobileParty.MainParty?.MapEvent;
            if (mapEvent == null) return "RaidSpawn mapEvent=<null>";

            var sb = new StringBuilder();
            sb.Append("RaidSpawn isRaid=").Append(mapEvent.IsRaid)
              .Append(" settlement='").Append(mapEvent.MapEventSettlement?.Name?.ToString() ?? "<none>").Append('\'');
            AppendSide(sb, mapEvent, BattleSideEnum.Attacker, "ATK");
            AppendSide(sb, mapEvent, BattleSideEnum.Defender, "DEF");
            return sb.ToString();
        }
        catch { return "RaidSpawn dump=<failed>"; }
    }

    private static void AppendSide(StringBuilder sb, MapEvent mapEvent, BattleSideEnum side, string label)
    {
        try
        {
            var es = mapEvent.GetMapEventSide(side);
            sb.Append(" | ").Append(label).Append(' ');
            if (es == null) { sb.Append("<null>"); return; }

            int involved = mapEvent.GetNumberOfInvolvedMen(side);
            sb.Append("involved=").Append(involved)
              .Append(" leader='").Append(es.LeaderParty?.Name?.ToString() ?? "<none>").Append('\'')
              .Append(" mainAmong=").Append(es.IsMainPartyAmongParties())
              .Append(" parties=[");

            var parties = es.Parties;
            int count = parties?.Count ?? 0;
            int shown = 0;
            if (parties != null)
            {
                foreach (var mep in parties)
                {
                    if (shown >= 8) break;
                    var p = mep?.Party;
                    if (shown > 0) sb.Append(", ");
                    bool isMain = p == PartyBase.MainParty;
                    sb.Append(p?.Name?.ToString() ?? "<null>")
                      .Append(isMain ? "*" : "")
                      .Append(":h").Append(p?.NumberOfHealthyMembers ?? -1)
                      .Append("/a").Append(p?.NumberOfAllMembers ?? -1);
                    shown++;
                }
            }
            if (count > shown) sb.Append(", +").Append(count - shown).Append(" more");
            sb.Append(']');
        }
        catch { sb.Append(" | ").Append(label).Append(" <err>"); }
    }
}
