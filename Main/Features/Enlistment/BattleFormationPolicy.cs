using TaleWorlds.Core;
using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment;

/// <summary>
/// Maps the enlisted soldier's <see cref="ServiceAssignment"/> to the battlefield formation
/// they belong in (#441). Pure table so the mapping is testable without a mission.
///
/// Support is deliberately unmapped: the rear-echelon fantasy has no line to stand in, and
/// forcing a Steward-track soldier into the shield wall would punish the assignment choice.
/// Gating (EnlistedBattle + not leading the side) is shared with the #424 role strip via
/// <see cref="BattleCommandPolicy.ShouldStripPlayerCommand"/> so the two cannot gate apart.
///
/// CORRECTION (2026-08-09, post-merge review): calling these "the two halves of the same engine
/// branch" overstates it. BehaviorComponent v1.4.7 :105 has FOUR conjuncts — neither role,
/// IsPlayerTroopInFormation, AND <c>Mission.Current.MainAgent != null</c> — and the flag was
/// already set by vanilla before this feature ran (see the behavior's header). Sharing the gate is
/// still right; the branch is not something this delivers.
/// </summary>
public static class BattleFormationPolicy
{
    public static FormationClass? TargetFormationFor(ServiceAssignment assignment)
    {
        switch (assignment)
        {
            case ServiceAssignment.Infantry: return FormationClass.Infantry;
            case ServiceAssignment.Archer: return FormationClass.Ranged;
            case ServiceAssignment.Cavalry: return FormationClass.Cavalry;
            default: return null;
        }
    }
}
