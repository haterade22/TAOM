using System;
using TAOM.Features.TroopProgression;

namespace TAOM.Features.Enlistment.Content;

public interface ISkillCheckService
{
    /// <summary>
    /// The duty skill check: best skill value + trust bonus (positive trust only, ×2)
    /// + rank bonus + roll(0..50) vs difficulty. Secondary skill (best-of-two) covers the
    /// donor's TrainRecruits shape.
    /// </summary>
    bool Passes(int primarySkillValue, int? secondarySkillValue, int trust, int rankBonus, int difficulty);
}

public class SkillCheckService : ISkillCheckService
{
    public const int RollRange = 51; // 0..50 inclusive

    /// <summary>
    /// Check bonus per rank level. Both duty systems alias THIS constant, so the magnitude
    /// cannot drift.
    ///
    /// It does NOT make the two curves identical, and the difference is deliberate: a field
    /// duty always applies the bonus, while an interactive option applies it only when its
    /// row sets <c>rankBonusApplies</c>. Rank always helps work you are ordered to do; it
    /// does not always help a personal choice in camp.
    /// </summary>
    public const int RankBonusPerLevel = 4;

    private readonly IRandomProvider _random;

    public SkillCheckService(IRandomProvider random)
    {
        _random = random;
    }

    public bool Passes(int primarySkillValue, int? secondarySkillValue, int trust, int rankBonus, int difficulty)
    {
        var skill = Math.Max(primarySkillValue, secondarySkillValue ?? int.MinValue);
        var trustBonus = Math.Max(0, trust) * 2;
        var roll = _random.Next(RollRange);
        return skill + trustBonus + rankBonus + roll >= difficulty;
    }
}
