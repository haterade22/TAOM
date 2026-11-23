using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem;

public interface ICareerRegistry
{
    CareerDefinition GetCareer(string careerStringId);
    IReadOnlyList<CareerDefinition> GetAllCareers();

    /// <summary>#1046: true when the career XML declares this id, even if its element failed to parse.</summary>
    bool IsCareerDeclared(string careerStringId);
    CareerChoiceDefinition GetChoice(string choiceStringId);
    CareerChoiceGroupDefinition GetGroup(string groupStringId);
    IReadOnlyList<CareerChoiceDefinition> GetChoicesForGroup(string groupStringId);

    /// <summary>
    /// The id of the career that owns <paramref name="choiceStringId"/>, resolved through the
    /// choice's group, or <c>null</c> when the choice, its group, or the owning career cannot be
    /// resolved.
    ///
    /// A null answer means "unknown", NEVER "belongs to nobody" — the two are indistinguishable
    /// from the registry's point of view, and a partially-loaded registry produces null for every
    /// choice in the game. Any caller acting destructively on the result must treat null as
    /// "leave it alone".
    /// </summary>
    string GetOwningCareerId(string choiceStringId);
    bool IsEligible(string careerStringId, ICareerHeroAdapter hero);

    /// <summary>
    /// The choice budget (root plus career points) at <paramref name="heroLevel"/>. A career that
    /// declares <c>PointLevels</c> grants one point per listed level reached; any other career,
    /// including a null one, gets the legacy one point per level.
    /// </summary>
    int GetMaxChoicesForHero(CareerDefinition career, int heroLevel);

    /// <summary>
    /// Issue #379 — unspent career points: max(0, budget-for-level − taken). The single
    /// source for the character-screen badge and the career screen's "Free Points" readout;
    /// clamps for saves whose taken count exceeds the current budget (post-rebalance).
    /// </summary>
    int GetUnspentPoints(CareerDefinition career, int heroLevel, int takenChoiceCount);

    /// <summary>
    /// The first of the career's point levels above <paramref name="heroLevel"/>, or null for a
    /// legacy career (a point every level) or when every point level has been reached.
    /// </summary>
    int? GetNextPointLevel(CareerDefinition career, int heroLevel);

    /// <summary>
    /// #769 E7: gold price of a retrain, and of leaving a career that holds fork picks:
    /// base + per-level × hero level (a level below 1 prices as level 1).
    /// </summary>
    int GetRetrainCost(int heroLevel);
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
