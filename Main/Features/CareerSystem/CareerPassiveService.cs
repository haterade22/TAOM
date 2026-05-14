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
        // Phase 9b #173 F2 — build the new dict OUTSIDE the lock, then swap under the lock.
        // Reads can briefly capture the OLD reference and finish their work without contention.
        var nextCache = new Dictionary<string, Dictionary<PassiveEffectType, float>>();

        var allData = dataService.GetAllData();
        _logger.LogInfo($"CareerSystem: Refreshing passive cache for {allData.Count} heroes");
        foreach (var kvp in allData)
        {
            var heroId = kvp.Key;
            var heroData = kvp.Value;
            if (string.IsNullOrEmpty(heroData.CareerStringId)) continue;

            var effectMap = new Dictionary<PassiveEffectType, float>();

            foreach (var choiceId in heroData.ChoiceIds)
            {
                var choice = registry.GetChoice(choiceId);
                if (choice?.Passive == null) continue;

                var passive = choice.Passive;
                if (effectMap.TryGetValue(passive.EffectType, out var existing))
                    effectMap[passive.EffectType] = existing + passive.Magnitude;
                else
                    effectMap[passive.EffectType] = passive.Magnitude;
            }

            // Also check root choice
            var career = registry.GetCareer(heroData.CareerStringId);
            if (career != null)
            {
                var rootChoice = registry.GetChoice(career.RootChoiceId);
                if (rootChoice?.Passive != null && !heroData.HasChoice(career.RootChoiceId))
                {
                    // Root choice passive is always active even if not in ChoiceIds
                    var passive = rootChoice.Passive;
                    if (effectMap.TryGetValue(passive.EffectType, out var existing))
                        effectMap[passive.EffectType] = existing + passive.Magnitude;
                    else
                        effectMap[passive.EffectType] = passive.Magnitude;
                }
            }

            if (effectMap.Count > 0)
            {
                nextCache[heroId] = effectMap;
                _logger.LogDebug($"CareerSystem: Cached {effectMap.Count} passives for hero '{heroId}' (career: {heroData.CareerStringId})");
            }
        }

        lock (_lock)
        {
            _cache = nextCache;
        }
        _logger.LogInfo($"CareerSystem: Passive cache complete — {nextCache.Count} heroes with active passives");
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
        if (magnitude != 0f)
            _logger.LogDebug($"CareerSystem: GetPassiveMagnitude hero='{heroStringId}' type={type} = {magnitude}");
        return magnitude;
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
