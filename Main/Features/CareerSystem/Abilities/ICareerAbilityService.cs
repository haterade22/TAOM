using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.Abilities;

public interface ICareerAbilityService
{
    CareerAbility GetOrCreateAbility(string heroStringId, ICareerRegistry registry, ICareerDataService dataService);
    void Tick(string heroStringId, float dt);
    bool IsAbilityReady(string heroStringId);
    float GetCooldownRemaining(string heroStringId);
    void ActivateAbility(string heroStringId);
    void ClearAll();
}
