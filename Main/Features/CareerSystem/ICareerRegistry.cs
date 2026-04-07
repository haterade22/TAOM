using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem;

public interface ICareerRegistry
{
    CareerDefinition GetCareer(string careerStringId);
    IReadOnlyList<CareerDefinition> GetAllCareers();
    CareerChoiceDefinition GetChoice(string choiceStringId);
    CareerChoiceGroupDefinition GetGroup(string groupStringId);
    IReadOnlyList<CareerChoiceDefinition> GetChoicesForGroup(string groupStringId);
    bool IsEligible(string careerStringId, ICareerHeroAdapter hero);
    int GetMaxChoicesForHero(int heroLevel);
    bool IsTierAvailable(int heroLevel, int tier);
}
