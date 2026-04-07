using TAOM.Adapters;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.Mutations;

public interface IMutationService
{
    AbilityTemplateData MutateAbility(
        AbilityTemplateData template,
        ICareerHeroAdapter hero,
        ICareerDataService dataService,
        ICareerRegistry registry);
}
