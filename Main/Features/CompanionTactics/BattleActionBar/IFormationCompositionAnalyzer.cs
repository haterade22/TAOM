using TAOM.Adapters;
using TAOM.Features.CompanionTactics.BattleActionBar.Models;

namespace TAOM.Features.CompanionTactics.BattleActionBar;

public interface IFormationCompositionAnalyzer
{
    FormationComposition Analyze(IFormationAdapter formation);
}
