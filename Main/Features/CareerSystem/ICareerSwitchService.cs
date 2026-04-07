using TAOM.Adapters;

namespace TAOM.Features.CareerSystem;

public interface ICareerSwitchService
{
    bool CanSwitch(ICareerHeroAdapter hero, string newCareerStringId);
    bool SwitchCareer(string heroStringId, ICareerHeroAdapter hero, string newCareerStringId);
}
