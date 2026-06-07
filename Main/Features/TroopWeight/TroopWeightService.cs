using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace TAOM.Features.TroopWeight;

public class TroopWeightService : ITroopWeightService
{
    private readonly IModLogger _logger;
    private readonly ITroopWeightXmlLoader _xmlLoader;
    private Dictionary<string, float> _weights;

    // Per-party weighted (healthy, wounded) cache for the display surfaces. GetWeightedHealthAndWounded
    // is called on the nameplate path (PartyBaseHelper.GetPartySizeText) for every visible party each
    // refresh, and an O(n) roster walk per call adds up. Reference-keyed + auto-evicting on party GC
    // (no GetHashCode collisions, no unbounded growth — unlike the hashcode-dict caches in the count hooks).
    private readonly ConditionalWeakTable<PartyBase, WeightedHealthBox> _healthCache = new();

    private sealed class WeightedHealthBox
    {
        public int Version = -1; // VersionNo is >= 0, so -1 forces a compute on first access
        public int Healthy;
        public int Wounded;
    }

    public TroopWeightService(IModLogger logger, ITroopWeightXmlLoader xmlLoader)
    {
        _logger = logger;
        _xmlLoader = xmlLoader;
        _weights = xmlLoader.GetTroopWeights();
        _logger.LogInfo($"[TroopWeight] Service initialized with {_weights.Count} weighted troop definitions");
    }

    public float GetTroopWeight(string troopStringId)
    {
        if (string.IsNullOrEmpty(troopStringId))
            return 1.0f;

        return _weights.TryGetValue(troopStringId, out var weight) ? weight : 1.0f;
    }

    public float GetTroopWeight(CharacterObject character)
    {
        return GetTroopWeight(character?.StringId);
    }

    public float CalculateWeightedMemberCount(PartyBase party)
    {
        if (party?.MemberRoster == null)
            return 0f;

        return CalculateWeightedRosterCount(party.MemberRoster);
    }

    public float CalculateWeightedRosterCount(TroopRoster roster)
    {
        if (roster == null || roster.Count <= 0)
            return 0f;

        try
        {
            float totalWeight = 0f;
            int count = roster.Count;
            for (int i = 0; i < count; i++)
            {
                var element = roster.GetElementCopyAtIndex(i);
                totalWeight += CalculateWeightedElementCount(element);
            }
            return totalWeight;
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[TroopWeight] Roster iteration failed (count={roster?.Count}): {ex.GetType().Name}: {ex.Message}");
            return 0f;
        }
    }

    public float CalculateWeightedElementCount(TroopRosterElement element)
    {
        if (element.Character == null)
            return element.Number;

        var weight = GetTroopWeight(element.Character);
        return element.Number * weight;
    }

    // Weighted contribution of one roster element. Shared by the pure (testable) and the
    // roster-walking (cached) entry points so their arithmetic can never drift apart.
    // Separate-ceiling note: ComputeWeightedHealthyAndWounded ceilings Healthy and Wounded
    // independently, matching PartyVMPopulatePartyListLabelHook. For integer weights (what TAOM
    // ships) Healthy + Wounded == the weighted member total exactly. With fractional weights and
    // mixed wound states the two ceilings can sum to 1 above Ceiling(total) — a cosmetic-only,
    // intentional consistency with the existing party-list label.
    private static (float Healthy, float Wounded) WeightedContribution(float weight, int number, int woundedNumber)
    {
        int wounded = woundedNumber < 0 ? 0 : woundedNumber;
        int healthy = number - wounded;
        if (healthy < 0)
            healthy = 0;

        return (healthy * weight, wounded * weight);
    }

    public (int Healthy, int Wounded) ComputeWeightedHealthyAndWounded(
        IEnumerable<(string TroopId, int Number, int WoundedNumber)> elements)
    {
        if (elements == null)
            return (0, 0);

        float weightedHealthy = 0f;
        float weightedWounded = 0f;

        foreach (var e in elements)
        {
            var (h, w) = WeightedContribution(GetTroopWeight(e.TroopId), e.Number, e.WoundedNumber);
            weightedHealthy += h;
            weightedWounded += w;
        }

        return ((int)Math.Ceiling(weightedHealthy), (int)Math.Ceiling(weightedWounded));
    }

    public (int Healthy, int Wounded) GetWeightedHealthAndWounded(PartyBase party)
    {
        if (party?.MemberRoster == null)
            return (0, 0);

        try
        {
            var roster = party.MemberRoster;
            int version = roster.VersionNo;
            var box = _healthCache.GetOrCreateValue(party);
            if (box.Version == version)
                return (box.Healthy, box.Wounded);

            // Walk the roster directly (no intermediate collection) — this is the nameplate hot path.
            float weightedHealthy = 0f;
            float weightedWounded = 0f;
            int count = roster.Count;
            for (int i = 0; i < count; i++)
            {
                var element = roster.GetElementCopyAtIndex(i);
                var (h, w) = WeightedContribution(GetTroopWeight(element.Character), element.Number, element.WoundedNumber);
                weightedHealthy += h;
                weightedWounded += w;
            }

            box.Version = version;
            box.Healthy = (int)Math.Ceiling(weightedHealthy);
            box.Wounded = (int)Math.Ceiling(weightedWounded);
            return (box.Healthy, box.Wounded);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[TroopWeight] GetWeightedHealthAndWounded failed (count={party?.MemberRoster?.Count}): {ex.GetType().Name}: {ex.Message}");
            return (0, 0);
        }
    }

    public void ClearCache()
    {
        _weights = _xmlLoader.GetTroopWeights();
    }
}
