using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem;

public class CareerPassiveService : ICareerPassiveService
{
    private readonly IModLogger _logger;

    // Phase 9b #173 F2 — snapshot-swap pattern + lock-on-mutation to fix the data race the
    // sister service FormationLayoutService already locks for. RefreshCache builds a brand-new
    // Dictionary and swaps the field under the lock; reads take the lock briefly to capture
    // a stable reference, then operate on the (immutable from their POV) snapshot lock-free.
    // Several callers can fire from AI worker threads (party-desertion model, party-size model).
    private readonly object _lock = new object();
    private Dictionary<string, Dictionary<PassiveEffectType, float>> _cache
        = new Dictionary<string, Dictionary<PassiveEffectType, float>>();

    // Per-(type, authored-mask) breakdown, populated alongside _cache. Lets the damage path honor
    // attack_type_mask (a "+X% ranged damage" pip only fires on ranged hits) while _cache keeps the
    // mask-agnostic per-type total every other consumer reads. Swapped under the same lock as _cache.
    private Dictionary<string, Dictionary<PassiveEffectType, Dictionary<AttackTypeMask, float>>> _maskedCache
        = new Dictionary<string, Dictionary<PassiveEffectType, Dictionary<AttackTypeMask, float>>>();

    // Phase 9b #173 — cached localization string for the ApplyFactor/Flat methods. Was duplicated
    // in CareerPassiveHelper; consolidated here so deletion of the helper doesn't leak the text.
    private static TextObject? _careerText;
    private static TextObject CareerText => _careerText ?? (_careerText = new TextObject("{=taom_career}Career"));

    public CareerPassiveService(IModLogger logger)
    {
        _logger = logger;
    }

    public void RefreshCache(ICareerDataService dataService, ICareerRegistry registry)
    {
        // Phase 9b #173 F2 — build the new dicts OUTSIDE the lock, then swap under the lock.
        // Reads can briefly capture the OLD reference and finish their work without contention.
        var nextCache = new Dictionary<string, Dictionary<PassiveEffectType, float>>();
        var nextMaskedCache = new Dictionary<string, Dictionary<PassiveEffectType, Dictionary<AttackTypeMask, float>>>();

        var allData = dataService.GetAllData();
        _logger.LogInfo($"CareerSystem: Refreshing passive cache for {allData.Count} heroes");
        foreach (var kvp in allData)
        {
            var heroId = kvp.Key;
            var heroData = kvp.Value;
            if (string.IsNullOrEmpty(heroData.CareerStringId)) continue;

            var effectMap = new Dictionary<PassiveEffectType, float>();
            var maskedMap = new Dictionary<PassiveEffectType, Dictionary<AttackTypeMask, float>>();

            foreach (var choiceId in heroData.ChoiceIds)
            {
                var choice = registry.GetChoice(choiceId);
                if (choice?.Passive != null)
                    Accumulate(effectMap, maskedMap, choice.Passive);
            }

            // Root choice passive is always active even if not in ChoiceIds.
            var career = registry.GetCareer(heroData.CareerStringId);
            if (career != null)
            {
                var rootChoice = registry.GetChoice(career.RootChoiceId);
                if (rootChoice?.Passive != null && !heroData.HasChoice(career.RootChoiceId))
                    Accumulate(effectMap, maskedMap, rootChoice.Passive);
            }

            if (effectMap.Count > 0)
            {
                nextCache[heroId] = effectMap;
                nextMaskedCache[heroId] = maskedMap;
                _logger.LogDebug($"CareerSystem: Cached {effectMap.Count} passives for hero '{heroId}' (career: {heroData.CareerStringId})");
            }
        }

        lock (_lock)
        {
            _cache = nextCache;
            _maskedCache = nextMaskedCache;
        }
        _logger.LogInfo($"CareerSystem: Passive cache complete — {nextCache.Count} heroes with active passives");
    }

    // Accumulates one passive into both the mask-agnostic per-type total and the per-(type, mask)
    // breakdown. Shared by the choice loop + the root-choice path so the two stay in sync.
    private static void Accumulate(
        Dictionary<PassiveEffectType, float> effectMap,
        Dictionary<PassiveEffectType, Dictionary<AttackTypeMask, float>> maskedMap,
        PassiveEffect passive)
    {
        var type = passive.EffectType;
        effectMap[type] = effectMap.TryGetValue(type, out var existing)
            ? existing + passive.Magnitude
            : passive.Magnitude;

        if (!maskedMap.TryGetValue(type, out var byMask))
            maskedMap[type] = byMask = new Dictionary<AttackTypeMask, float>();
        var mask = passive.AttackTypeMask;
        byMask[mask] = byMask.TryGetValue(mask, out var m) ? m + passive.Magnitude : passive.Magnitude;
    }

    public float GetPassiveMagnitude(string heroStringId, PassiveEffectType type)
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

    public float GetMaskedMagnitude(string heroStringId, PassiveEffectType type, AttackTypeMask hitMask)
    {
        if (string.IsNullOrEmpty(heroStringId)) return 0f;

        Dictionary<string, Dictionary<PassiveEffectType, Dictionary<AttackTypeMask, float>>> snapshot;
        lock (_lock) { snapshot = _maskedCache; }

        if (!snapshot.TryGetValue(heroStringId, out var typeMap)) return 0f;
        if (!typeMap.TryGetValue(type, out var byMask)) return 0f;

        // Sum every authored-mask bucket whose delivery type intersects the hit (All-masked
        // entries carry both bits, so they apply to any hit).
        var total = 0f;
        foreach (var entry in byMask)
            if ((entry.Key & hitMask) != AttackTypeMask.None)
                total += entry.Value;
        return total;
    }

    public bool HasActivePassive(string heroStringId, PassiveEffectType type)
    {
        if (string.IsNullOrEmpty(heroStringId)) return false;
        Dictionary<string, Dictionary<PassiveEffectType, float>> snapshot;
        lock (_lock) { snapshot = _cache; }
        return snapshot.TryGetValue(heroStringId, out var effectMap)
            && effectMap.ContainsKey(type);
    }

    // Phase 9b #173 — instance methods replacing the static CareerPassiveHelper.ApplyFactor /
    // ApplyFlat. Per ADR-007 accept primitive `string heroStringId` (boundary GameModels extract
    // `hero?.StringId` at the call site). Null/empty/zero-magnitude all short-circuit.
    public void ApplyFactor(string heroStringId, ref ExplainedNumber result, PassiveEffectType type)
    {
        if (string.IsNullOrEmpty(heroStringId)) return;
        var magnitude = GetPassiveMagnitude(heroStringId, type);
        if (magnitude != 0f)
            result.AddFactor(magnitude, CareerText);
    }

    public void ApplyFlat(string heroStringId, ref ExplainedNumber result, PassiveEffectType type)
    {
        if (string.IsNullOrEmpty(heroStringId)) return;
        var magnitude = GetPassiveMagnitude(heroStringId, type);
        if (magnitude != 0f)
            result.Add(magnitude, CareerText);
    }
}
