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
/// <see cref="BattleCommandPolicy.ShouldStripPlayerCommand"/> — the two corrections are the
/// two halves of the same engine branch (BehaviorComponent v1.4.7 :105: neither-role AND
/// IsPlayerTroopInFormation), so they must gate identically or the fantasy tears in half.
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
