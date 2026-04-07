using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem;

public interface ICareerPassiveService
{
    void RefreshCache(ICareerDataService dataService, ICareerRegistry registry);
    float GetPassiveMagnitude(string heroStringId, PassiveEffectType type);
    bool HasActivePassive(string heroStringId, PassiveEffectType type);
}
