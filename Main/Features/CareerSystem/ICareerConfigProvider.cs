using System.Collections.Generic;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem;

public interface ICareerConfigProvider
{
    IReadOnlyList<CareerDefinition> LoadCareers();
    IReadOnlyList<CareerChoiceGroupDefinition> LoadChoiceGroups();
    IReadOnlyList<CareerChoiceDefinition> LoadChoices();
    int GetMaxPerkPoints();
}
