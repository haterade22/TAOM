using System;
using System.Collections.Generic;

namespace TAOM.Features.FieldCommission.Domain;

/// <summary>
/// Pure calculator: turns a troop template's level + skill values into a
/// <see cref="CommissionSkillPlan"/> for the companion the troop is promoted into.
///
/// Design (TAOM-original — the donor mod copied template skills verbatim with no cap, which for
/// a high-tier elite troop template would hand a fresh companion master-level skills instantly):
/// hero level = troop level (clamped to at least 1); each skill is capped to
/// <c>min(maxSkillValue, heroLevel * skillPointsPerLevel)</c>, negative template values clamp to 0.
/// One focus point is granted per non-zero skill and a flat bonus is granted to every core
/// attribute — both applied by the adapter via <c>HeroDeveloper.AddFocus</c>/<c>AddAttribute</c>
/// with <c>checkUnspentFocusPoints:false</c>/<c>checkUnspentPoints:false</c> (fresh initialization,
/// not spending from an existing pool).
/// </summary>
public class CommissionSkillBudget
{
    private const int DefaultFocusPerNonZeroSkill = 1;
    private const int DefaultFlatAttributeBonus = 2;

    public CommissionSkillPlan Compute(
        int troopLevel,
        IReadOnlyDictionary<string, int> troopSkillValues,
        int skillPointsPerLevel,
        int maxSkillValue)
    {
        var heroLevel = Math.Max(1, troopLevel);
        var effectivePointsPerLevel = Math.Max(1, skillPointsPerLevel);
        var budget = Math.Min(Math.Max(0, maxSkillValue), heroLevel * effectivePointsPerLevel);

        var skillValues = new Dictionary<string, int>();
        if (troopSkillValues != null)
        {
            foreach (var pair in troopSkillValues)
            {
                var clampedTemplateValue = Math.Max(0, pair.Value);
                skillValues[pair.Key] = Math.Min(clampedTemplateValue, budget);
            }
        }

        return new CommissionSkillPlan(heroLevel, skillValues, DefaultFocusPerNonZeroSkill, DefaultFlatAttributeBonus);
    }
}
