using System.Collections.Generic;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem;

public class CareerPassiveService : ICareerPassiveService
{
    private Dictionary<string, Dictionary<PassiveEffectType, float>> _cache
        = new Dictionary<string, Dictionary<PassiveEffectType, float>>();

    public void RefreshCache(ICareerDataService dataService, ICareerRegistry registry)
    {
        _cache.Clear();

        var allData = dataService.GetAllData();
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
                _cache[heroId] = effectMap;
        }
    }

    public float GetPassiveMagnitude(string heroStringId, PassiveEffectType type)
    {
        if (!_cache.TryGetValue(heroStringId, out var effectMap)) return 0f;
        return effectMap.TryGetValue(type, out var magnitude) ? magnitude : 0f;
    }

    public bool HasActivePassive(string heroStringId, PassiveEffectType type)
    {
        return _cache.TryGetValue(heroStringId, out var effectMap)
            && effectMap.ContainsKey(type);
    }
}
