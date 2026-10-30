using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem;

public class CareerPassiveService : ICareerPassiveService
{
    private readonly IModLogger _logger;
    private readonly IPartySizeCacheInvalidator _partySizeCache;

    // Phase 9b #173 F2 — snapshot-swap pattern + lock-on-mutation to fix the data race the
    // sister service FormationLayoutService already locks for. RefreshCache builds a brand-new
    // Dictionary and swaps the field under the lock; reads take the lock briefly to capture
    // a stable reference, then operate on the (immutable from their POV) snapshot lock-free.
    // Several callers can fire from AI worker threads (party-desertion model, party-size model).
    private readonly object _lock = new object();
    // The UNFILTERED per-type total: every passive of the type summed, whatever its attack_type_mask or
    // troop_class. This is what every non-combat consumer reads (party size, wages, speed...).
    private Dictionary<string, Dictionary<PassiveEffectType, float>> _cache
        = new Dictionary<string, Dictionary<PassiveEffectType, float>>();

    // Per-(type, authored-mask, authored-troop-class) breakdown, populated alongside _cache. Lets the
    // damage path honor attack_type_mask (a "+X% ranged damage" pip only fires on ranged hits) and
    // troop_class (a "+X% infantry damage" pip only reaches infantry) while _cache keeps the total
    // every other consumer reads. Swapped under the same lock as _cache. The buckets are walked with the
    // Dictionary's struct enumerator, so a read allocates nothing.
    private Dictionary<string, Dictionary<PassiveEffectType, Dictionary<(AttackTypeMask Mask, TroopClassMask TroopClass), float>>> _maskedCache
        = new Dictionary<string, Dictionary<PassiveEffectType, Dictionary<(AttackTypeMask Mask, TroopClassMask TroopClass), float>>>();

    // Phase 9b #173 — cached localization string for the ApplyFactor/Flat methods. Was duplicated
    // in CareerPassiveHelper; consolidated here so deletion of the helper doesn't leak the text.
    private static TextObject? _careerText;
    private static TextObject CareerText => _careerText ?? (_careerText = new TextObject("{=taom_career}Career"));

    // Below this the accumulated factor frame has cancelled the value to (or past) nothing and
    // converting a flat count into it is meaningless. Matches TroopWeightService.MinFactorScale.
    private const float MinFactorScale = 0.01f;

    public CareerPassiveService(IModLogger logger, IPartySizeCacheInvalidator partySizeCache)
    {
        _logger = logger;
        _partySizeCache = partySizeCache;
    }

    public void RefreshCache(ICareerDataService dataService, ICareerRegistry registry)
    {
        // Phase 9b #173 F2 — build the new dicts OUTSIDE the lock, then swap under the lock.
        // Reads can briefly capture the OLD reference and finish their work without contention.
        var nextCache = new Dictionary<string, Dictionary<PassiveEffectType, float>>();
        var nextMaskedCache = new Dictionary<string, Dictionary<PassiveEffectType, Dictionary<(AttackTypeMask Mask, TroopClassMask TroopClass), float>>>();

        var allData = dataService.GetAllData();
        _logger.LogInfo($"CareerSystem: Refreshing passive cache for {allData.Count} heroes");
        foreach (var kvp in allData)
        {
            var heroId = kvp.Key;
            var heroData = kvp.Value;
            if (string.IsNullOrEmpty(heroData.CareerStringId)) continue;

            var effectMap = new Dictionary<PassiveEffectType, float>();
            var maskedMap = new Dictionary<PassiveEffectType, Dictionary<(AttackTypeMask Mask, TroopClassMask TroopClass), float>>();

            foreach (var choiceId in heroData.ChoiceIds)
            {
                var choice = registry.GetChoice(choiceId);
                if (choice != null)
                    Accumulate(effectMap, maskedMap, choice.Passives);
            }

            // Root choice passive is always active even if not in ChoiceIds.
            var career = registry.GetCareer(heroData.CareerStringId);
            if (career != null)
            {
                var rootChoice = registry.GetChoice(career.RootChoiceId);
                if (rootChoice != null && !heroData.HasChoice(career.RootChoiceId))
                    Accumulate(effectMap, maskedMap, rootChoice.Passives);
            }

            if (effectMap.Count > 0)
            {
                nextCache[heroId] = effectMap;
                nextMaskedCache[heroId] = maskedMap;
                _logger.LogDebug($"CareerSystem: Cached {effectMap.Count} passives for hero '{heroId}' (career: {heroData.CareerStringId})");
            }
        }

        Dictionary<string, Dictionary<PassiveEffectType, float>> previousCache;
        lock (_lock)
        {
            previousCache = _cache;
            _cache = nextCache;
            _maskedCache = nextMaskedCache;
        }
        _logger.LogInfo($"CareerSystem: Passive cache complete — {nextCache.Count} heroes with active passives");

        // PartyBase.PartySizeLimit caches on MemberRoster.VersionNo, so a PartySize pick would not show
        // until an unrelated roster event (#768). Only a party a career hero leads can change, and a hero
        // whose passives disappeared must refresh too, hence the union of the old and new cache. The
        // invalidation must never fail a refresh.
        try
        {
            var affected = new HashSet<string>(previousCache.Keys);
            affected.UnionWith(nextCache.Keys);
            _partySizeCache.InvalidateLedBy(affected);
        }
        catch (System.Exception ex)
        {
            _logger.LogWarning($"CareerSystem: party-size cache invalidation failed after passive refresh: {ex.Message}");
        }
    }

    // Accumulates a choice's passives (one or two, #769 E8) into both the unfiltered per-type total and
    // the per-(type, mask, class) breakdown. Shared by the choice loop + the root-choice path so the two
    // stay in sync.
    private static void Accumulate(
        Dictionary<PassiveEffectType, float> effectMap,
        Dictionary<PassiveEffectType, Dictionary<(AttackTypeMask Mask, TroopClassMask TroopClass), float>> maskedMap,
        IReadOnlyList<PassiveEffect> passives)
    {
        foreach (var passive in passives)
        {
            var type = passive.EffectType;
            effectMap[type] = effectMap.TryGetValue(type, out var existing)
                ? existing + passive.Magnitude
                : passive.Magnitude;

            if (!maskedMap.TryGetValue(type, out var byBucket))
                maskedMap[type] = byBucket = new Dictionary<(AttackTypeMask Mask, TroopClassMask TroopClass), float>();
            var bucket = (passive.AttackTypeMask, passive.TroopClass);
            byBucket[bucket] = byBucket.TryGetValue(bucket, out var m) ? m + passive.Magnitude : passive.Magnitude;
        }
    }

    public float GetPassiveMagnitude(string? heroStringId, PassiveEffectType type)
    {
        if (string.IsNullOrEmpty(heroStringId)) return 0f;

        // Phase 9b #173 F2 — capture the cache reference under the lock, then operate on the
        // captured snapshot lock-free. RefreshCache may concurrently swap _cache to a new
        // instance, but our captured reference remains stable.
        Dictionary<string, Dictionary<PassiveEffectType, float>> snapshot;
        lock (_lock) { snapshot = _cache; }

        if (!snapshot.TryGetValue(heroStringId, out var effectMap)) return 0f;
        if (!effectMap.TryGetValue(type, out var magnitude)) return 0f;
        // NO per-lookup logging here. This is a hot path: since 2026-08-06 it is reached from
        // TaomCharacterStatsModel.MaxHitpoints (every agent spawn, every tooltip, the daily heal
        // tick) and from CalculateDamageAmplification PER BLOW for every non-hero troop whose
        // leader holds a TroopDamage pip. The old `if (magnitude != 0f) LogDebug($"...")` line
        // allocated on EVERY such call — interpolation is eager, so the string is built before
        // LogDebug is entered, and FileLogger has no level gate (LogDebug -> Enqueue does a
        // DateTime.Now.ToString + a second interpolation + an unbounded queue push). A large
        // battle turned that into thousands of allocations and log lines per minute, which also
        // buries the crash-triage log TAOM relies on for native CTDs.
        // The cache CONTENTS are still recoverable: RefreshCache logs them per hero (rarely).
        return magnitude;
    }

    // Every authored-mask bucket the hit matches (All-masked entries carry both bits, so they apply to any
    // hit), whatever its troop class: the troop reading with all three classes, so the two walks cannot drift.
    public float GetMaskedMagnitude(string? heroStringId, PassiveEffectType type, AttackTypeMask hitMask)
        => GetTroopMagnitude(heroStringId, type, TroopClassMask.All, hitMask);

    public float GetTroopMagnitude(string? heroStringId, PassiveEffectType type, TroopClassMask troopClass, AttackTypeMask? hitMask = null)
    {
        if (string.IsNullOrEmpty(heroStringId)) return 0f;

        Dictionary<string, Dictionary<PassiveEffectType, Dictionary<(AttackTypeMask Mask, TroopClassMask TroopClass), float>>> snapshot;
        lock (_lock) { snapshot = _maskedCache; }

        if (!snapshot.TryGetValue(heroStringId, out var typeMap)) return 0f;
        if (!typeMap.TryGetValue(type, out var byBucket)) return 0f;

        // A bucket counts when its mask matches the hit (no hit mask: any mask but None, the loader's
        // sentinel for an unparseable one, which no hit matches either) AND its class is All or shares a
        // bit with the troop's. A troop of class None shares no bit, so it still gets the All-class
        // buckets and nothing narrower. Hot path (per blow, per non-hero troop): no logging, and the
        // struct enumerator allocates nothing.
        var total = 0f;
        foreach (var entry in byBucket)
        {
            var bucket = entry.Key;
            if (hitMask.HasValue ? !AttackTypeMaskMatch.Matches(bucket.Mask, hitMask.Value) : bucket.Mask == AttackTypeMask.None) continue;
            if (bucket.TroopClass != TroopClassMask.All && (bucket.TroopClass & troopClass) == TroopClassMask.None) continue;
            total += entry.Value;
        }
        return total;
    }

    public bool HasActivePassive(string? heroStringId, PassiveEffectType type)
    {
        if (string.IsNullOrEmpty(heroStringId)) return false;
        Dictionary<string, Dictionary<PassiveEffectType, float>> snapshot;
        lock (_lock) { snapshot = _cache; }
        return snapshot.TryGetValue(heroStringId, out var effectMap)
            && effectMap.ContainsKey(type);
    }

    // What a reader takes for a type: the unfiltered total, except for a class-filtered type (#769 E9), where a
    // class-only pip ("+10% infantry damage") would otherwise count at full magnitude for a consumer that has no
    // class. Those read the pips that reach troopClass; left at None (a reader with no troop) a troop of class None
    // gets the All-class buckets and nothing narrower, the army-wide pips. The raid speed (TaomRaidModel) is the one
    // such reader of TroopDamage; the training model (TroopTraining) passes the roster element's class.
    private float Magnitude(string? heroStringId, PassiveEffectType type, TroopClassMask troopClass = TroopClassMask.None)
        => PassiveEffectConsumers.IsClassFiltered(type)
            ? GetTroopMagnitude(heroStringId, type, troopClass)
            : GetPassiveMagnitude(heroStringId, type);

    // Phase 9b #173 — instance methods replacing the static CareerPassiveHelper.ApplyFactor /
    // ApplyFlat. Per ADR-007 accept primitive `string heroStringId` (boundary GameModels extract
    // `hero?.StringId` at the call site). Null/empty/zero-magnitude all short-circuit.
    public void ApplyFactor(string? heroStringId, ref ExplainedNumber result, PassiveEffectType type)
    {
        if (string.IsNullOrEmpty(heroStringId)) return;
        var magnitude = Magnitude(heroStringId, type);
        if (magnitude != 0f)
        {
            var before = result.ResultNumber;
            result.AddFactor(magnitude, CareerText);
            LogEvent(type, heroStringId, magnitude, before, result.ResultNumber);
        }
    }

    // #613: the event-scoped types (a battle end, an upgrade, a daily tick) get a line; the per-tick ones
    // (speed, morale, wages, range) are read off their ExplainedNumber by taom.career_perks instead, a line per
    // tick would drown the log. #769 E11: a line that records no change is skipped. Vanilla pays 0 influence
    // outside a kingdom and a factor on 0 is 0, so "InfluenceGain +10% ...: 0 -> 0" would print after every
    // minor battle and teach a tester to ignore the one line that matters.
    private void LogEvent(PassiveEffectType type, string heroStringId, float magnitude, float before, float after)
    {
        if (!Diagnostics.CareerPerkLogPolicy.IsEventScoped(type) || before == after) return;
        _logger.LogInfo($"[CareerPerks] {type} {(magnitude >= 0f ? "+" : "")}{magnitude * 100f:0.#}% for '{heroStringId}': {before:0.##} -> {after:0.##}");
    }

    public void ApplyFlat(string? heroStringId, ref ExplainedNumber result, PassiveEffectType type, TroopClassMask troopClass = TroopClassMask.None)
    {
        if (string.IsNullOrEmpty(heroStringId)) return;
        AddFlat(ref result, Magnitude(heroStringId, type, troopClass));
    }

    // #769 E11: a model method that returns a float (or one share of a list) has no ExplainedNumber to put a
    // factor on, so the scale is applied directly. A scale below zero would flip a penalty into a discount under
    // the base price, so it floors at zero: a magnitude of -1 or less is the full cut, with no cliff at -1. The
    // loader keeps NaN and infinity out of the cache, so the scale is always a finite number.
    public float ScaleByPassive(string? heroStringId, float value, PassiveEffectType type)
    {
        if (string.IsNullOrEmpty(heroStringId)) return value;
        var magnitude = Magnitude(heroStringId, type);
        if (magnitude == 0f) return value;

        var scaled = value * System.Math.Max(0f, 1f + magnitude);
        LogEvent(type, heroStringId, magnitude, value, scaled);
        return scaled;
    }

    // #769 E11: vanilla's daily healing is negative for a starving party (-regulars/4) or a raft party whose -0.25 per regular outweighs its healing, and
    // +1 on the isPrisoner call (the captor healing its prisoners). A plus factor on the first would wound the
    // troops faster, and the second is not the leader's own troops: the factor only speeds a real recovery. A total
    // below -1 would turn that recovery into a daily wound (vanilla sets no lower limit), so the factor stops at -1.
    public void ApplyWoundRecovery(string? heroStringId, bool isPrisoner, ref ExplainedNumber result)
    {
        if (isPrisoner || !(result.ResultNumber > 0f) || string.IsNullOrEmpty(heroStringId)) return;
        var magnitude = System.Math.Max(Magnitude(heroStringId, PassiveEffectType.WoundRecovery), -1f);
        if (magnitude != 0f) result.AddFactor(magnitude, CareerText);
    }

    private static void AddFlat(ref ExplainedNumber result, float magnitude)
    {
        if (magnitude == 0f) return;

        // A flat passive is authored as a COUNT and the career screen shows it as one ("+4 party
        // size", magnitude="4"), so it must be worth exactly four bodies. ExplainedNumber.Add lands
        // in the BASE frame and the result is BaseNumber * (1 + SumOfFactors), so a raw Add is
        // multiplied by every factor already on that number, whatever the call order. With only a
        // culture feat in play that was a rounding error; once AiPartySize started contributing a
        // large factor (garrisons run at 3x by default) the same +4 became +13, and the promise on
        // the career screen stopped being true. Divide the factor back out so the count is literal.
        // The division cancels only the factors ALREADY on the number: a factor added after this call
        // multiplies the count again. Callers must therefore call it after the last AddFactor
        // (TaomPartySizeModel does, pinned by AiPartySizeOrderingTests).
        // Deep review 2026-08-18; same idiom as TroopWeightService.SubtractResultFramePenalty.
        float scale = 1f + result.SumOfFactors;

        // Positive requirement, so NaN fails it: below this the factor frame has cancelled the
        // number to nothing (or flipped its sign) and dividing by it is meaningless.
        if (!(scale > MinFactorScale)) return;

        result.Add(magnitude / scale, CareerText);
    }
}
