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

    /// <summary>Check bonus per rank level. Shared by interactive and field duties so the two
    /// difficulty curves cannot drift apart.</summary>
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
