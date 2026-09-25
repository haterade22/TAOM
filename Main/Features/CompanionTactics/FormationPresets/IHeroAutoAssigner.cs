using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Features.CompanionTactics.FormationPresets.Models;
using TAOM.Features.CompanionTactics.Roles.Models;

namespace TAOM.Features.CompanionTactics.FormationPresets;

/// <summary>
/// Role-based scoring used by the OOB auto-assign button. Returns a 0..100 affinity score
/// for putting a hero into a formation of class <paramref name="formationClass"/>.
///
/// formationClass values are vanilla TaleWorlds.Core.DeploymentFormationClass:
///   0=Unset, 1=Infantry, 2=Ranged, 3=Cavalry, 4=HorseArcher, 5=InfantryAndRanged, 6=CavalryAndHorseArcher
/// </summary>
public interface IHeroAutoAssigner
{
    int ScoreHeroForFormation(IHeroCombatAdapter hero, int formationClass);
    int ScoreRoleForFormation(CombatRole role, int formationClass);

    /// <summary>
    /// Plans which candidate leads which open formation. Global greedy on
    /// <see cref="ScoreHeroForFormation"/>: pairs with score above 0, highest score first, ties
    /// broken by lower slot index then lower hero index; each hero and each slot used at most
    /// once. Slots whose class is 0 (Unset) are never filled. Null lists or null heroes are
    /// skipped. Returned in the order the pairs were taken.
    /// </summary>
    IReadOnlyList<CaptainAssignment> PlanCaptains(
        IReadOnlyList<IHeroCombatAdapter> heroes, IReadOnlyList<int> formationClasses);
}
