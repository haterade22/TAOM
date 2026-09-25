using TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle;
using TAOM.Features.CompanionTactics.FormationPresets.Models;

namespace TAOM.Features.CompanionTactics.FormationPresets;

/// <summary>
/// Applies Auto-Assign to the live Order of Battle view model. Boundary class: it exposes the
/// TaleWorlds VM type because its only callers (the overlay VM) live at the boundary, as
/// <see cref="IOrderOfBattleVMTracker"/> does; that VM's constructor needs a running
/// <c>Game.Current</c>. The matching itself is <see cref="IHeroAutoAssigner.PlanCaptains"/>.
/// </summary>
public interface IOOBCaptainAutoAssigner
{
    AutoAssignResult AssignCaptains(OrderOfBattleVM vm);
}
