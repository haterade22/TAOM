using System;
using System.Collections.Generic;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Features.TroopWeight.Hooks;

// Boundary for shed-on-upgrade. Reads the (sealed) party roster into engine-free WeightedTroopEntry
// rows, delegates the decision to the pure ITroopWeightService.PlanShed, then applies the removals.
// All TaleWorlds types stay in this class (ADR-002/007); the arithmetic lives in the unit-tested service.
public class PartyUpgraderUpgradeReadyTroopsHook : IOnPartyUpgraderUpgradeReadyTroops
{
    private readonly ITroopWeightService _troopWeightService;
    private readonly IModLogger _logger;

    public PartyUpgraderUpgradeReadyTroopsHook(ITroopWeightService troopWeightService, IModLogger logger)
    {
        _troopWeightService = troopWeightService;
        _logger = logger;
    }

    public void OnUpgradeReadyTroops(PartyBase party)
    {
        try
        {
            // Mirror vanilla UpgradeReadyTroops' own guard: never trim the player party or inactive parties.
            if (party == null || party == PartyBase.MainParty || !party.IsActive)
                return;

            var roster = party.MemberRoster;
            if (roster == null || roster.Count <= 0)
                return;

            int limit = party.PartySizeLimit;

            // Cheap early-out: only build entries + plan when the weighted total actually exceeds the cap.
            float weighted = _troopWeightService.CalculateWeightedMemberCount(party);
            if (weighted <= limit)
                return;

            int count = roster.Count;
            var entries = new List<WeightedTroopEntry>(count);
            var byId = new Dictionary<string, CharacterObject>(count);
            for (int i = 0; i < count; i++)
            {
                var element = roster.GetElementCopyAtIndex(i);
                var ch = element.Character;
                if (ch == null || string.IsNullOrEmpty(ch.StringId) || element.Number <= 0)
                    continue;

                entries.Add(new WeightedTroopEntry(
                    ch.StringId, ch.Tier, _troopWeightService.GetTroopWeight(ch), element.Number, ch.IsHero));
                byId[ch.StringId] = ch;
            }

            var plan = _troopWeightService.PlanShed(entries, limit);
            if (plan.Count == 0)
                return;

            int shedTotal = 0;
            foreach (var instr in plan)
            {
                if (instr.Count > 0 && byId.TryGetValue(instr.TroopId, out var ch))
                {
                    roster.AddToCounts(ch, -instr.Count);
                    shedTotal += instr.Count;
                }
            }

            // [diag] event-gated (fires only when a shed happens) — proves the right parties trim in-game.
            // Strip after sign-off per the comprehensive-diag-then-remove rule.
            if (shedTotal > 0)
                _logger.LogInfo(
                    $"[TroopWeight][diag] Shed {shedTotal} bodies from '{party.Name}' " +
                    $"(weighted {weighted:0.#} > limit {limit}); kept elites.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[TroopWeight] Shed-on-upgrade failed: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
