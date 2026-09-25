using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Features.CompanionTactics.FormationPresets.Models;
using TAOM.Features.CompanionTactics.Roles;
using TAOM.Features.CompanionTactics.Roles.Models;

namespace TAOM.Features.CompanionTactics.FormationPresets;

/// <summary>
/// Role-to-formation affinity scoring. Mirrors the original developer's <c>GetRoleMatchScore</c>
/// switch so behavior parity holds for save-import users.
///
/// formationClass values are vanilla TaleWorlds.Core.DeploymentFormationClass: 0=Unset,
/// 1=Infantry, 2=Ranged, 3=Cavalry, 4=HorseArcher, 5=InfantryAndRanged, 6=CavalryAndHorseArcher.
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
            5 => melee ? 100 : (ranged ? 50 : 0),    // InfantryAndRanged: prefers melee, accepts ranged
            6 => cavalry ? 100 : (horseArcher ? 50 : 0),
            0 => 50,                                  // Unset
            _ => 50,
        };
    }

    private const int UnsetFormationClass = 0;

    public IReadOnlyList<CaptainAssignment> PlanCaptains(
        IReadOnlyList<IHeroCombatAdapter> heroes, IReadOnlyList<int> formationClasses)
    {
        var plan = new List<CaptainAssignment>();
        if (heroes == null || formationClasses == null) return plan;

        var pairs = new List<(int Score, int Slot, int Hero)>();
        for (var slot = 0; slot < formationClasses.Count; slot++)
        {
            var formationClass = formationClasses[slot];
            if (formationClass == UnsetFormationClass) continue;
            for (var hero = 0; hero < heroes.Count; hero++)
            {
                var score = ScoreHeroForFormation(heroes[hero], formationClass);
                if (score > 0) pairs.Add((score, slot, hero));
            }
        }

        pairs.Sort((a, b) => a.Score != b.Score ? b.Score.CompareTo(a.Score)
            : a.Slot != b.Slot ? a.Slot.CompareTo(b.Slot)
            : a.Hero.CompareTo(b.Hero));

        var usedHeroes = new bool[heroes.Count];
        var usedSlots = new bool[formationClasses.Count];
        foreach (var pair in pairs)
        {
            if (usedHeroes[pair.Hero] || usedSlots[pair.Slot]) continue;
            usedHeroes[pair.Hero] = usedSlots[pair.Slot] = true;
            plan.Add(new CaptainAssignment(pair.Hero, pair.Slot));
        }
        return plan;
    }
}
