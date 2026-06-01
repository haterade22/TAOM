using System.Collections.Generic;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem;

/// <summary>Loads + validates the career-quest definitions from <c>taom_career_quests.xml</c>.</summary>
public interface ICareerQuestConfigProvider
{
    IReadOnlyList<CareerQuestDefinition> LoadQuests();
}
