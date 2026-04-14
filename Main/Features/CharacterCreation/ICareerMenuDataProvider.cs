using System.Collections.Generic;
using TAOM.Features.CharacterCreation.Models;

namespace TAOM.Features.CharacterCreation;

public interface ICareerMenuDataProvider
{
    IReadOnlyList<CareerMenuOptionDefinition> LoadCareerMenuOptions();
    CareerMenuOptionDefinition GetOptionForCareer(string careerStringId);
}
