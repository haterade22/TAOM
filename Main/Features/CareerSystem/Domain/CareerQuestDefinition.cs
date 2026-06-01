using System.Collections.Generic;

namespace TAOM.Features.CareerSystem.Domain;

/// <summary>
/// One career tier quest: completing its objectives unlocks the named tier of the career's choice
/// tree (hybrid with the level gate) and applies its rewards. Immutable; authored in
/// <c>taom_career_quests.xml</c>, one per (career, tier) at most. Careers/tiers with no authored
/// quest simply fall through to the level gate.
/// </summary>
public sealed class CareerQuestDefinition
{
    public string Id { get; }

    /// <summary>The career this quest belongs to (matches a <see cref="CareerDefinition.Id"/>).</summary>
    public string CareerId { get; }

    /// <summary>Which choice-tree tier (1-3) this quest unlocks on completion.</summary>
    public int Tier { get; }

    /// <summary>Localized quest title, e.g. "{=key}The Captain's Charge". Used for the journal + offer inquiry.</summary>
    public string TitleKey { get; }

    /// <summary>Localized quest description/flavour text for the offer inquiry + journal body.</summary>
    public string DescriptionKey { get; }

    public IReadOnlyList<CareerQuestObjectiveDefinition> Objectives { get; }

    public IReadOnlyList<CareerQuestRewardDefinition> Rewards { get; }

    public CareerQuestDefinition(
        string id,
        string careerId,
        int tier,
        string titleKey,
        string descriptionKey,
        IReadOnlyList<CareerQuestObjectiveDefinition> objectives,
        IReadOnlyList<CareerQuestRewardDefinition> rewards)
    {
        Id = id;
        CareerId = careerId;
        Tier = tier;
        TitleKey = titleKey ?? "";
        DescriptionKey = descriptionKey ?? "";
        Objectives = objectives ?? new List<CareerQuestObjectiveDefinition>();
        Rewards = rewards ?? new List<CareerQuestRewardDefinition>();
    }
}
