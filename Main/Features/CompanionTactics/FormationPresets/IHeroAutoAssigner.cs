using TAOM.Adapters;
using TAOM.Features.CompanionTactics.Roles.Models;

namespace TAOM.Features.CompanionTactics.FormationPresets;

/// <summary>
/// Role-based scoring used by the OOB auto-assign button. Returns a 0..130 affinity score
/// for putting a hero into a formation of class <paramref name="formationClass"/>.
///
/// formationClass values follow vanilla DeploymentFormationClass:
///   0=Default, 1=Infantry, 2=Ranged, 3=Cavalry, 4=HorseArcher, 5=HeavyInfantry, 6=LightCavalry
/// </summary>
public interface IHeroAutoAssigner
{
    int ScoreHeroForFormation(IHeroCombatAdapter hero, int formationClass);
    int ScoreRoleForFormation(CombatRole role, int formationClass);
}
