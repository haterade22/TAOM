using TAOM.Adapters;
using TAOM.Features.CompanionTactics.Roles;
using TAOM.Features.CompanionTactics.Roles.Models;

namespace TAOM.Features.CompanionTactics.FormationPresets;

/// <summary>
/// Role-to-formation affinity scoring. Mirrors the original developer's <c>GetRoleMatchScore</c>
/// switch so behavior parity holds for save-import users.
///
/// formationClass values: 0=Default, 1=Infantry, 2=Ranged, 3=Cavalry, 4=HorseArcher,
/// 5=HeavyInfantry, 6=LightCavalry. Source values from <c>DeploymentFormationClass</c>.
/// </summary>
public sealed class HeroAutoAssigner : IHeroAutoAssigner
{
    private readonly ICompanionRoleService _roles;

    public HeroAutoAssigner(ICompanionRoleService roles)
    {
        _roles = roles;
    }

    public int ScoreHeroForFormation(IHeroCombatAdapter hero, int formationClass)
    {
        if (hero == null) return 0;
        var role = _roles.GetPrimaryRole(hero);
        return ScoreRoleForFormation(role, formationClass);
    }

    public int ScoreRoleForFormation(CombatRole role, int formationClass)
    {
        var melee = role == CombatRole.ShieldInfantry || role == CombatRole.TwoHanded
                 || role == CombatRole.Polearm || role == CombatRole.OneHanded;
        var ranged = role == CombatRole.Archer || role == CombatRole.Crossbow
                  || role == CombatRole.Skirmisher || role == CombatRole.Slinger;
        var cavalry = role == CombatRole.Cavalry;
        var horseArcher = role == CombatRole.HorseArcher;

        return formationClass switch
        {
            1 => melee ? 100 : 0,
            2 => ranged ? 100 : 0,
            3 => cavalry ? 100 : 0,
            4 => horseArcher ? 100 : 0,
            5 => melee ? 100 : (ranged ? 50 : 0),    // HeavyInfantry — prefers melee, accepts ranged
            6 => cavalry ? 100 : (horseArcher ? 50 : 0),
            0 => 50,                                  // Default class
            _ => 50,
        };
    }
}
