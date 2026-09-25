using TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle;
using TAOM.Features.CompanionTactics.FormationPresets.Models;

namespace TAOM.Features.CompanionTactics.FormationPresets;

/// <summary>
/// Applies Auto-Assign to the live Order of Battle view model. Boundary class: exposes a
/// TaleWorlds VM type that cannot be constructed outside a mission, because its only callers
/// (the overlay VM) live at the boundary, as <see cref="IOrderOfBattleVMTracker"/> does. The
/// matching itself is <see cref="IHeroAutoAssigner.PlanCaptains"/>.
/// </summary>
public interface IOOBCaptainAutoAssigner
{
    AutoAssignResult AssignCaptains(OrderOfBattleVM vm);
}
