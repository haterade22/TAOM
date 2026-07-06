namespace TAOM.Features.CareerSystem.Domain;

/// <summary>
/// What completing a career quest grants. Each value maps to an application branch in
/// <c>CareerQuestService.ApplyRewards</c>. Add one (input, branch) test per value.
/// </summary>
public enum CareerQuestRewardType
{
    /// <summary>
    /// Unlock the choice-tree tier named by <see cref="CareerQuestRewardDefinition.Amount"/> (1-3) for the hero,
    /// in addition to the level gate (the hybrid mechanic). The headline reward of a tier quest.
    /// Applied via the existing <c>ICareerDataService.UnlockTier</c> (persisted in <c>_taom_careerTiers</c>).
    /// </summary>
    UnlockTier,

    /// <summary>Grant the item whose id is in <see cref="CareerQuestRewardDefinition.Param"/> (Amount = count) to the hero's inventory.</summary>
    GrantItem,

    /// <summary>Grant <see cref="CareerQuestRewardDefinition.Amount"/> renown to the hero's clan.</summary>
    GrantRenown,

    /// <summary>Grant <see cref="CareerQuestRewardDefinition.Amount"/> influence to the hero's clan.</summary>
    GrantInfluence,

    /// <summary>
    /// Set a string flag in the hero's career data, used to gate downstream dialog/content.
    /// Flag name in <see cref="CareerQuestRewardDefinition.Param"/>.
    /// </summary>
    GrantAttributeFlag
}

/// <summary>
/// One reward applied on career-quest completion. Immutable; authored in
/// <c>taom_career_quests.xml</c> and validated before use.
/// </summary>
public sealed class CareerQuestRewardDefinition
{
    public CareerQuestRewardType Type { get; }

    /// <summary>Numeric payload: tier number (UnlockTier), point/renown/influence/relation count, or item count. Sign-checked per type at load.</summary>
    public int Amount { get; }

    /// <summary>String payload: item id / companion id / hero id / flag name. Empty when the type needs no string.</summary>
    public string Param { get; }

    public CareerQuestRewardDefinition(CareerQuestRewardType type, int amount, string param)
    {
        Type = type;
        Amount = amount;
        Param = param ?? "";
    }
}
