using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace TAOM.Features.BanditManagement.Hooks;

/// <summary>
/// Patch86 (assault route) — Prefix, skip-original, on the public static
/// <c>Helpers.MapEventHelper.GetPriorityListForHideoutMission(List&lt;MobileParty&gt;, out int)</c>
/// (v1.4.8 :147-172; its single caller is <c>SandBoxMissions.OpenHideoutBattleMission</c> :1142,
/// once per daytime hideout assault).
///
/// The method returns the phase-1 priority list and writes the phase-1 count; every healthy troop
/// NOT in that list is what <c>HideoutMissionController.SpawnBossAndBodyguards</c> (:789) spawns
/// beside the boss, because <c>PartyGroupTroopSupplier.NumTroopsNotSupplied</c> is simply the
/// initial count minus what phase 1 took. Vanilla's split is
/// <c>firstPhase = min(floor(0.8 * total), FirstFightMax)</c>, so the boss phase is a fifth of the
/// hideout (and, on an existing save, the whole untrimmed boss party). This replacement holds back
/// exactly the boss(es) plus <see cref="IHideoutBossFightService.BodyguardCount"/> regulars.
///
/// REPLICATED from vanilla (harmony-il.md: a skip-original prefix inherits every gate): the
/// healthy total (:149), the per-party flatten (:152-156), wounded removed first (:157), heroes
/// and <c>Culture.BanditBoss</c> troops all held back and counted AFTER the wounded pass (:158),
/// the highest-level regulars taken next (:163), the phase-1 roster returned (:171).
/// DROPPED: the 0.8 / FirstFightMax formula (:150), which is the change, and four
/// <c>Debug.Print</c> lines (:167-170), replaced by one TAOM info line.
///
/// FAIL-OPEN IS SAFE HERE. Nothing above the decision mutates campaign state: the roster is a
/// fresh copy, and the parties are only read. Vanilla's own split is therefore a working default
/// at this call site, and the catch hands the call back; the cost is one hideout with the old
/// boss-fight size, logged.
///
/// <c>firstPhaseTroopCount</c> is <c>out</c> on the target; Harmony passes it by reference either
/// way, and <c>ref</c> lets the deferral path leave it to the original. Both parameter names are
/// pinned by <c>Patch86HideoutBossFightBindingTests</c> because Harmony binds them by name.
/// </summary>
[HarmonyPatch(typeof(MapEventHelper), nameof(MapEventHelper.GetPriorityListForHideoutMission))]
[HarmonyPatchCategory(Patch86_HideoutBossFight.Category)]
public static class Patch86_HideoutAssaultBossFight
{
    [HarmonyPrefix]
    public static bool Prefix(List<MobileParty> partyList, ref int firstPhaseTroopCount, ref FlattenedTroopRoster __result)
    {
        try
        {
            var service = Patch86_HideoutBossFight.Service;
            if (service == null || partyList == null)
                return true;

            var healthyTotal = 0;
            foreach (var party in partyList)
                healthyTotal += party.Party.MemberRoster.TotalHealthyCount;

            var roster = new FlattenedTroopRoster(healthyTotal);
            foreach (var party in partyList)
                roster.Add(party.Party.MemberRoster.GetTroopRoster());
            roster.RemoveIf(x => x.IsWounded);

            // Boundary conversion (ADR-007): the service sees levels and flags, never an element.
            var elements = roster.ToList();
            var candidates = new List<HideoutTroopCandidate>(elements.Count);
            foreach (var element in elements)
            {
                var troop = element.Troop;
                var isHeroOrBoss = troop.IsHero || troop.Culture?.BanditBoss == troop;
                candidates.Add(new HideoutTroopCandidate(troop.Level, isHeroOrBoss, element.IsWounded));
            }

            var split = service.PlanAssault(candidates);

            var keep = new HashSet<UniqueTroopDescriptor>();
            foreach (var index in split.FirstPhaseIndices)
                keep.Add(elements[index].Descriptor);
            roster.RemoveIf(x => !keep.Contains(x.Descriptor));

            firstPhaseTroopCount = split.FirstPhaseTroopCount;
            __result = roster;

            Patch86_HideoutBossFight.Logger?.LogInfo(
                $"[Patch86] hideout assault split: first phase {split.FirstPhaseTroopCount}, boss phase " +
                $"{split.BossPhaseTroopCount} of {elements.Count} healthy (bodyguards {service.BodyguardCount})");
            return false;
        }
        catch (Exception ex)
        {
            try
            {
                Patch86_HideoutBossFight.Logger?.LogError(
                    $"[Patch86] assault split failed, deferring to vanilla's own split for this hideout: {ex}");
            }
            catch { /* never throw out of a prefix */ }
            return true;
        }
    }
}
