using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Localization;

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

    // Cached so the per-limit-query ExplainedNumber annotation doesn't allocate a TextObject each call.
    private static readonly TextObject HeavyTroopsText = new("{=taom_troop_weight_size}Heavy troops");

    // The pre-penalty (true) party-size limit captured the last time ApplyPartySizeWeightPenalty ran for a
    // party, so the shed hook can recover it EXACTLY. A clamped deflated limit is lossy — a clamp floors it
    // at 1 regardless of the true base, so `deflated + surplus` overshoots and the shed under-trims exactly
    // the elite-heavy parties it exists to police (deep-review 2026-07-11, data-flow GAP#1). Reference-keyed
    // + GC-evicting like _healthCache.
    private readonly ConditionalWeakTable<PartyBase, StrongBox<int>> _lastBaseLimit = new();

    public void ApplyPartySizeWeightPenalty(PartyBase party, ref ExplainedNumber limit)
    {
        if (!(TaomSettings.Instance?.EnableTroopWeight ?? true))
            return;
        if (party?.MemberRoster == null)
            return;

        // Guard the CAST, not just the arithmetic after it: a non-finite ResultNumber (another mod's
        // ExplainedNumber mutation, a future negative-factor feat) casts to int.MinValue, which would be
        // cached as this party's "true base" and handed to the shed planner as an astronomically large
        // budget — shedding the party's whole roster. Bail to vanilla instead. (deep-review 2026-07-17.)
        if (!FiniteFloatValidator.IsFinite(limit.ResultNumber))
            return;

        int baseLimit = (int)limit.ResultNumber;   // pre-penalty true base — remembered for the shed hook
        _lastBaseLimit.GetOrCreateValue(party).Value = baseLimit;

        int raw = party.MemberRoster.TotalManCount;
        int weighted = (int)Math.Ceiling(CalculateWeightedMemberCount(party));
        int penalty = ComputeSizePenalty(raw, weighted, baseLimit);
        SubtractResultFramePenalty(ref limit, penalty);
    }

    // Below this, a culture's factors have cancelled the limit to (or past) nothing and dividing by the
    // scale is meaningless or sign-flipping. Not a "small number" epsilon — the gate is about the frame
    // being usable at all.
    private const float MinFactorScale = 0.01f;

    /// <summary>
    /// Subtracts a RESULT-frame penalty (an absolute body count) from <paramref name="limit"/>.
    /// <see cref="ExplainedNumber.Add"/> mutates <c>BaseNumber</c>, and the result is
    /// <c>BaseNumber * (1 + SumOfFactors)</c> — so a raw <c>Add(-penalty)</c> is silently amplified to
    /// <c>penalty * (1 + SumOfFactors)</c> for any factor-boosted culture (call order is irrelevant;
    /// factors are summed, not sequenced). Dividing the factor back out makes the subtraction cost exactly
    /// <paramref name="resultFramePenalty"/> slots, and restores <see cref="ComputeSizePenalty"/>'s
    /// "limit never drops below 1" floor, which that clamp computes in the result frame.
    /// Engine-free; unit-tested.
    /// </summary>
    public static void SubtractResultFramePenalty(ref ExplainedNumber limit, int resultFramePenalty)
    {
        if (resultFramePenalty <= 0)
            return;

        // Positive requirement, per the engine-float gate rule: NaN fails this, an inverted
        // `scale <= 0` early-out would let it through and poison the limit with NaN.
        float scale = 1f + limit.SumOfFactors;
        if (!(scale > MinFactorScale))
            return;

        limit.Add(-resultFramePenalty / scale, HeavyTroopsText);
    }

    public int GetTrueBaseSizeLimit(PartyBase party)
    {
        if (party == null)
            return 0;
        // Reading PartySizeLimit forces the engine to recompute GetPartyMemberSizeLimit on a roster-version
        // change (the shed runs right after an upgrade bumps the version), which re-runs
        // ApplyPartySizeWeightPenalty and refreshes the cached base for the current version.
        int deflated = party.PartySizeLimit;
        return _lastBaseLimit.TryGetValue(party, out var box) ? box.Value : deflated;
    }

    /// <summary>
    /// Pure clamp for the party-size weight penalty: the amount to subtract from a party's size limit so
    /// its raw count fills the cap at the troop weight. = <c>weightedCount − rawCount</c> (the weight
    /// surplus), never below 0, and never so large that the limit would drop below 1 (a party always
    /// holds at least its leader). Engine-free; unit-tested.
    /// </summary>
    public static int ComputeSizePenalty(int rawCount, int weightedCount, int baseLimit)
    {
        int penalty = weightedCount - rawCount;
        if (penalty <= 0)
            return 0;
        // Gate on baseLimit BEFORE the subtraction, not on `baseLimit - 1` after it. `(int)float.NaN` and
        // `(int)float.PositiveInfinity` are both int.MinValue on net472/x64, and `int.MinValue - 1`
        // underflows (unchecked) to int.MaxValue — so a `maxReducible <= 0` check computed AFTER the
        // subtraction reads a poisoned baseLimit as "reduce by up to int.MaxValue" and hands back a
        // plausible penalty. Requiring `baseLimit >= 2` up front makes the subtraction unreachable for
        // every degenerate value. (deep-review 2026-07-17, data-flow MED.)
        if (baseLimit < 2)
            return 0;
        int maxReducible = baseLimit - 1;
        return penalty > maxReducible ? maxReducible : penalty;
    }

    // Weighted contribution of one roster element. Shared by the pure (testable) and the
    // roster-walking (cached) entry points so their arithmetic can never drift apart.
    // Separate-ceiling note: ComputeWeightedHealthyAndWounded ceilings Healthy and Wounded
    // independently. For integer weights (what TAOM ships) Healthy + Wounded == the weighted member
    // total exactly. With fractional weights and mixed wound states the two ceilings can sum to 1
    // above Ceiling(total) — cosmetic only. (Only the special-currency count diagnostic consumes this
    // now; the weighted-display hooks that used it were removed in the 2026-07-11 count->limit rework.)
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

    public IReadOnlyList<ShedInstruction> PlanShed(IReadOnlyList<WeightedTroopEntry> entries, int limit)
    {
        var result = new List<ShedInstruction>();
        if (entries == null || entries.Count == 0)
            return result;

        // Weighted member total on the same basis as CalculateWeightedMemberCount (Number * weight, incl.
        // wounded) — the frame the shed compares against the party's true (pre-deflation) size limit.
        double weighted = 0d;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e.Count > 0)
                weighted += (double)e.Count * e.Weight;
        }

        double overflow = weighted - limit;
        if (overflow <= 0d)
            return result;

        // Shed lowest-value first: keep elites, trim the cheap fodder. Heroes are never sheddable.
        var sheddable = new List<WeightedTroopEntry>();
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (!e.IsHero && e.Count > 0 && e.Weight > 0f)
                sheddable.Add(e);
        }
        sheddable.Sort((a, b) =>
        {
            int byTier = a.Tier.CompareTo(b.Tier);
            return byTier != 0 ? byTier : a.Weight.CompareTo(b.Weight);
        });

        foreach (var e in sheddable)
        {
            if (overflow <= 0d)
                break;

            int bodiesNeeded = (int)Math.Ceiling(overflow / e.Weight);
            int shed = Math.Min(e.Count, bodiesNeeded);
            if (shed <= 0)
                continue;

            result.Add(new ShedInstruction(e.TroopId, shed));
            overflow -= (double)shed * e.Weight;
        }

        return result;
    }

    public void ClearCache()
    {
        _weights = _xmlLoader.GetTroopWeights();
    }
}
