using TAOM.Features.CultureDoctrine.Domain;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CultureDoctrine.Hooks;

/// <summary>
/// Keeps a routed formation out of every tactic's consolidation. Every field tactic, vanilla
/// and TAOM, folds the team's cavalry to two blocks through
/// <c>TacticComponent.SplitFormationClassIntoGivenNumber</c>, which skips a formation whose
/// <c>Formation.IsAIOwned</c> is false (`TacticComponent.cs:243`); the engine's own way to make
/// that false for an AI-controlled formation is
/// <c>SetControlledByAI(true, enforceNotSplittableByAI: true)</c> (`Formation.cs:326-347`, `:832`).
/// <c>SetControlledByAI</c> returns early when the control flag is unchanged (`:811`), so the
/// flag is set by toggling control off and on, which at <c>EarlyStart</c> touches an empty
/// formation with no active behaviour (`:819`) and no orders. A player-controlled formation is
/// left alone; <c>Team.DelegateCommandToAI</c> (F6) clears the flag again (`Team.cs:479-485`),
/// so on the player's team the guard holds only until the player delegates. The flag is read
/// by nothing else in the field (<c>IsSplittableByAI</c> serves the sergeant split and the
/// siege tactics). Main thread, once per team, before the first spawn.
/// </summary>
public static class RoutedFormationGuard
{
    public static int Protect(Team team, FormationRouting routing)
    {
        if (routing.IsEmpty)
            return 0;
        var protectedCount = 0;
        foreach (var formationClass in routing.Classes)
        {
            var formation = team.GetFormation(formationClass);
            if (formation == null || !formation.IsAIControlled)
                continue;
            formation.SetControlledByAI(isControlledByAI: false);
            formation.SetControlledByAI(isControlledByAI: true, enforceNotSplittableByAI: true);
            protectedCount++;
        }
        return protectedCount;
    }
}
