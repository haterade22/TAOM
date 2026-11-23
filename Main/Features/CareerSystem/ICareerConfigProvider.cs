using System.Collections.Generic;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem;

public interface ICareerConfigProvider
{
    IReadOnlyList<CareerDefinition> LoadCareers();

    /// <summary>#1046: true when taom_careers.xml has a &lt;Career&gt; element with this id, whether or
    /// not it parsed. A failed element is missing from <see cref="LoadCareers"/> but not retired.</summary>
    bool IsCareerDeclared(string careerId);
    IReadOnlyList<CareerChoiceGroupDefinition> LoadChoiceGroups();
    IReadOnlyList<CareerChoiceDefinition> LoadChoices();
    int GetMaxPerkPoints();

    /// <summary>#769 E7: flat part of the gold price of a retrain or a fork-holding career switch.</summary>
    int GetRetrainGoldBase();

    /// <summary>#769 E7: the per-hero-level part of that price.</summary>
    int GetRetrainGoldPerLevel();
    AbilityTemplateData GetAbilityTemplate(string templateId);
    AbilityTuningConfig GetAbilityTuning();
}
