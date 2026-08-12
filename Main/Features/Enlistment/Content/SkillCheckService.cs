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

    /// <summary>
    /// The skill the check actually uses: the better of the two support skills, never their sum.
    /// Public so a caller reporting the outcome quotes the same number the check consumed rather
    /// than re-deriving it and drifting.
    /// </summary>
    public static int EffectiveSkill(int primarySkillValue, int? secondarySkillValue)
        => Math.Max(primarySkillValue, secondarySkillValue ?? int.MinValue);

    /// <summary>
    /// Trust's contribution. Negative trust contributes nothing rather than subtracting, so a run
    /// of failures cannot dig a hole the check keeps counting against you. Public for the same
    /// no-drift reason as <see cref="EffectiveSkill"/>.
    /// </summary>
    public static int TrustBonus(int trust) => Math.Max(0, trust) * 2;

    public bool Passes(int primarySkillValue, int? secondarySkillValue, int trust, int rankBonus, int difficulty)
    {
        var roll = _random.Next(RollRange);
        return EffectiveSkill(primarySkillValue, secondarySkillValue)
            + TrustBonus(trust) + rankBonus + roll >= difficulty;
    }
}
