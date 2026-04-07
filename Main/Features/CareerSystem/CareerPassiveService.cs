using System.Collections.Generic;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem;

public class CareerPassiveService : ICareerPassiveService
{
    private readonly IModLogger _logger;

    private Dictionary<string, Dictionary<PassiveEffectType, float>> _cache
        = new Dictionary<string, Dictionary<PassiveEffectType, float>>();

    public CareerPassiveService(IModLogger logger)
    {
        _logger = logger;
    }

    public void RefreshCache(ICareerDataService dataService, ICareerRegistry registry)
    {
        _cache.Clear();

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
                _cache[heroId] = effectMap;
                _logger.LogDebug($"CareerSystem: Cached {effectMap.Count} passives for hero '{heroId}' (career: {heroData.CareerStringId})");
            }
        }
        _logger.LogInfo($"CareerSystem: Passive cache complete — {_cache.Count} heroes with active passives");
    }

    public float GetPassiveMagnitude(string heroStringId, PassiveEffectType type)
    {
        if (!_cache.TryGetValue(heroStringId, out var effectMap)) return 0f;
        if (!effectMap.TryGetValue(type, out var magnitude)) return 0f;
        if (magnitude != 0f)
            _logger.LogDebug($"CareerSystem: GetPassiveMagnitude hero='{heroStringId}' type={type} = {magnitude}");
        return magnitude;
    }

    public bool HasActivePassive(string heroStringId, PassiveEffectType type)
    {
        return _cache.TryGetValue(heroStringId, out var effectMap)
            && effectMap.ContainsKey(type);
    }
}
