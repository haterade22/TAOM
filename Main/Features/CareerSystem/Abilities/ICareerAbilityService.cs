using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.Abilities;

public interface ICareerAbilityService
{
    CareerAbility GetOrCreateAbility(string heroStringId, ICareerRegistry registry, ICareerDataService dataService);
    void AddCharge(string heroStringId, float amount, ChargeType sourceType);
    void Tick(string heroStringId, float dt);
    bool IsAbilityReady(string heroStringId);
    void ActivateAbility(string heroStringId);
    void ClearAll();
}
