using System;
using TAOM.Features.CultureDoctrine.Doctrines;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CultureDoctrine.Hooks.Tactics;

/// <summary>
/// Applies one <see cref="FormationPlan"/> to one <see cref="Formation"/>: vanilla's own
/// sequence (<c>ResetBehaviorWeights</c>, <c>SetDefaultBehaviorWeights</c>, then one
/// <c>SetBehaviorWeight&lt;T&gt;</c> per row) with the parameter each behaviour exposes set the way
/// vanilla's tactics set it. The switch is the only place a <see cref="BehaviorKind"/> meets a
/// concrete engine type. Runs on the team-AI tick inside the owning tactic's try/catch;
/// <c>SetBehaviorWeight&lt;T&gt;</c> throws <c>MBException</c> for a behaviour the formation was
/// never given, and a <see cref="BehaviorKind"/> with no case throws too, so a new enum member
/// cannot silently leave a behaviour at its default weight; both land in the failed-tactic
/// latch rather than a native unwind. <c>DoctrineSwitchInvariantTests</c> pins the switch.
/// </summary>
public static class BehaviorWeightApplier
{
    public static void Apply(Formation formation, FormationPlan plan, TaomTacticBase owner)
    {
        var ai = formation.AI;
        ai.ResetBehaviorWeights();
        TacticComponent.SetDefaultBehaviorWeights(formation);
        var weights = plan.Weights;
        for (var i = 0; i < weights.Count; i++)
        {
            var w = weights[i].Weight;
            switch (weights[i].Kind)
            {
                case BehaviorKind.Charge: ai.SetBehaviorWeight<BehaviorCharge>(w); break;
                case BehaviorKind.TacticalCharge: ai.SetBehaviorWeight<BehaviorTacticalCharge>(w); break;
                case BehaviorKind.Advance: ai.SetBehaviorWeight<BehaviorAdvance>(w); break;
                case BehaviorKind.CautiousAdvance: ai.SetBehaviorWeight<BehaviorCautiousAdvance>(w); break;
                case BehaviorKind.HoldHighGround: ai.SetBehaviorWeight<BehaviorHoldHighGround>(w).RangedAllyFormation = owner.Archers; break;
                case BehaviorKind.Defend: ai.SetBehaviorWeight<BehaviorDefend>(w).DefensePosition = owner.DefensePosition; break;
                case BehaviorKind.DefensiveRing: ai.SetBehaviorWeight<BehaviorDefensiveRing>(w).TacticalDefendPosition = owner.RingPosition; break;
                case BehaviorKind.FireFromInfantryCover: ai.SetBehaviorWeight<BehaviorFireFromInfantryCover>(w); break;
                case BehaviorKind.Skirmish: ai.SetBehaviorWeight<BehaviorSkirmish>(w); break;
                case BehaviorKind.SkirmishLine: ai.SetBehaviorWeight<BehaviorSkirmishLine>(w); break;
                case BehaviorKind.ScreenedSkirmish: ai.SetBehaviorWeight<BehaviorScreenedSkirmish>(w); break;
                case BehaviorKind.ProtectFlank:
                    ai.SetBehaviorWeight<BehaviorProtectFlank>(w).FlankSide =
                        plan.Role == FormationRole.RightCavalry ? FormationAI.BehaviorSide.Right : FormationAI.BehaviorSide.Left;
                    break;
                case BehaviorKind.CavalryScreen: ai.SetBehaviorWeight<BehaviorCavalryScreen>(w); break;
                case BehaviorKind.Flank: ai.SetBehaviorWeight<BehaviorFlank>(w); break;
                case BehaviorKind.Vanguard: ai.SetBehaviorWeight<BehaviorVanguard>(w); break;
                case BehaviorKind.HorseArcherSkirmish: ai.SetBehaviorWeight<BehaviorHorseArcherSkirmish>(w); break;
                case BehaviorKind.MountedSkirmish: ai.SetBehaviorWeight<BehaviorMountedSkirmish>(w); break;
                case BehaviorKind.PullBack: ai.SetBehaviorWeight<BehaviorPullBack>(w); break;
                case BehaviorKind.Regroup: ai.SetBehaviorWeight<BehaviorRegroup>(w); break;
                case BehaviorKind.Reserve: ai.SetBehaviorWeight<BehaviorReserve>(w); break;
                case BehaviorKind.Retreat: ai.SetBehaviorWeight<BehaviorRetreat>(w); break;
                case BehaviorKind.Stop: ai.SetBehaviorWeight<BehaviorStop>(w); break;
                default: throw new ArgumentOutOfRangeException(nameof(plan), weights[i].Kind, "no behaviour type for this kind");
            }
        }
    }
}
