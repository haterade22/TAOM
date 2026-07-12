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

    // One-shot breadcrumb: log the first shed event (with real values), then stay silent.
    // Static = once per game process, so the diag doesn't flood across upgrade ticks.
    private static bool _loggedShed;

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

            // 2026-07-11 rework: the weight penalty now DEFLATES the party-size limit rather than inflating
            // the count, so party.PartySizeLimit is the EFFECTIVE (deflated) cap and the count reads raw.
            // The party is over budget when its raw count exceeds the deflated limit (equivalent to the old
            // "weighted > base limit").
            int deflatedLimit = party.PartySizeLimit;
            int raw = roster.TotalManCount;
            if (raw <= deflatedLimit)
                return;

            // The weighted-frame planner needs the TRUE (pre-deflation) base. Ask the service for it rather
            // than reconstructing `deflated + surplus` — that overshoots whenever the penalty was clamped
            // (deep-review 2026-07-11 GAP#1), which is exactly the post-upgrade heavy party the shed targets.
            int limit = _troopWeightService.GetTrueBaseSizeLimit(party);
            float weighted = _troopWeightService.CalculateWeightedMemberCount(party);

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

            // [diag] one-shot breadcrumb: feature is confirmed working, so log only the FIRST
            // shed of the session (with real values) as a "this path is alive" marker, then stay
            // silent — the comprehensive-diag-then-remove rule's keep-one-line form.
            if (shedTotal > 0 && !_loggedShed)
            {
                _loggedShed = true;
                _logger.LogInfo(
                    $"[TroopWeight][diag] Shed {shedTotal} bodies from '{party.Name}' " +
                    $"(weighted {weighted:0.#} > limit {limit}); kept elites.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[TroopWeight] Shed-on-upgrade failed: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
