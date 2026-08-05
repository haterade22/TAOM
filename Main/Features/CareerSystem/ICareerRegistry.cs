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

    /// <summary>
    /// Issue #379 — unspent career points: max(0, budget-for-level − taken). The single
    /// source for the character-screen badge and the career screen's "Free Points" readout;
    /// clamps for saves whose taken count exceeds the current budget (post-rebalance).
    /// </summary>
    int GetUnspentPoints(int heroLevel, int takenChoiceCount);
    bool IsTierAvailable(int heroLevel, int tier);

    /// <summary>Hero level at which the given tier (1-3) unlocks. Returns int.MaxValue for unknown tiers.</summary>
    int GetTierUnlockLevel(int tier);

    /// <summary>
    /// Eligible careers a hero could switch INTO -- filtered by `IsEligible` AND excluding
    /// `currentCareerId`. Used by the "I wish to discuss my career path" dialogue option and the
    /// career-switch screen picker. Returns empty when no alternative exists (the dialogue option
    /// hides itself in that case).
    /// </summary>
    IReadOnlyList<CareerDefinition> GetEligibleSwitchTargets(string currentCareerId, ICareerHeroAdapter hero);
}
