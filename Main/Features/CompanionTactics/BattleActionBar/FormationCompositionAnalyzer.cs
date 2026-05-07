using TAOM.Adapters;
using TAOM.Features.CompanionTactics.BattleActionBar.Models;

namespace TAOM.Features.CompanionTactics.BattleActionBar;

/// <summary>
/// Pure read-only analyzer that maps a formation's unit-count breakdown to flags the
/// action-bar service consumes. "Dominant" thresholds: ≥ 60% of the formation is the class.
/// </summary>
public sealed class FormationCompositionAnalyzer : IFormationCompositionAnalyzer
{
    private const float DominantRatio = 0.6f;

    public FormationComposition Analyze(IFormationAdapter formation)
    {
        if (formation == null) return default;
        var total = formation.CountOfUnits;
        if (total <= 0) return default;

        var ranged = formation.RangedUnitCount;
        var cavalry = formation.CavalryUnitCount;
        var polearm = formation.PolearmUnitCount;
        var shield = formation.ShieldUnitCount;

        var rangedRatio = (float)ranged / total;
        var cavalryRatio = (float)cavalry / total;

        return new FormationComposition(
            hasRanged: ranged > 0,
            hasPolearm: polearm > 0,
            hasShields: shield > 0,
            hasCavalry: cavalry > 0,
            isRangedDominant: rangedRatio >= DominantRatio,
            isCavalryDominant: cavalryRatio >= DominantRatio);
    }
}
