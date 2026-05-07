using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Features.CompanionTactics.BattleActionBar.Models;

namespace TAOM.Features.CompanionTactics.BattleActionBar;

public interface IBattleActionBarService
{
    /// <summary>Resolve the contextual buttons for the formation, gated by MCM settings.</summary>
    IReadOnlyList<BattleAction> GetActionsForFormation(IFormationAdapter formation);
}
