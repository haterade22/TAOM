using System.Collections.Generic;

namespace TAOM.Features.FieldCommission.Domain;

/// <summary>
/// Pure output of <see cref="CommissionSkillBudget"/>: the hero level and per-skill values to
/// stamp onto a promoted companion, plus flat focus/attribute allocation counts. Skill keys are
/// skill StringIds (e.g. "OneHanded") — the adapter maps them back to <c>SkillObject</c>.
/// </summary>
public sealed class CommissionSkillPlan
{
    public CommissionSkillPlan(int heroLevel, IReadOnlyDictionary<string, int> skillValues, int focusPerNonZeroSkill, int flatAttributeBonus)
    {
        HeroLevel = heroLevel;
        SkillValues = skillValues;
        FocusPerNonZeroSkill = focusPerNonZeroSkill;
        FlatAttributeBonus = flatAttributeBonus;
    }

    public int HeroLevel { get; }

    /// <summary>Skill StringId -> capped value.</summary>
    public IReadOnlyDictionary<string, int> SkillValues { get; }

    /// <summary>Focus points granted to every skill whose capped value is above zero.</summary>
    public int FocusPerNonZeroSkill { get; }

    /// <summary>Flat bonus applied to every one of the hero's core attributes.</summary>
    public int FlatAttributeBonus { get; }
}
