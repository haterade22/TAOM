using System.Collections.Generic;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.Abilities;

public class CareerAbilityService : ICareerAbilityService
{
    private readonly Dictionary<string, CareerAbility> _abilities = new Dictionary<string, CareerAbility>();

    public CareerAbility GetOrCreateAbility(string heroStringId, ICareerRegistry registry, ICareerDataService dataService)
    {
        if (_abilities.TryGetValue(heroStringId, out var existing))
            return existing;

        var careerId = dataService.GetCareerStringId(heroStringId);
        if (string.IsNullOrEmpty(careerId)) return null;

        var career = registry.GetCareer(careerId);
        if (career == null) return null;

        var ability = new CareerAbility(
            career.AbilityTemplateId,
            career.ChargeType,
            career.MaxCharge,
            cooldownDuration: 10f);

        _abilities[heroStringId] = ability;
        return ability;
    }

    public void AddCharge(string heroStringId, float amount, ChargeType sourceType)
    {
        if (_abilities.TryGetValue(heroStringId, out var ability))
            ability.AddCharge(amount, sourceType);
    }

    public void Tick(string heroStringId, float dt)
    {
        if (_abilities.TryGetValue(heroStringId, out var ability))
            ability.Tick(dt);
    }

    public bool IsAbilityReady(string heroStringId)
    {
        return _abilities.TryGetValue(heroStringId, out var ability) && ability.IsReady;
    }

    public void ActivateAbility(string heroStringId)
    {
        if (_abilities.TryGetValue(heroStringId, out var ability))
            ability.Activate();
    }

    public void ClearAll()
    {
        _abilities.Clear();
    }
}
