using System;
using TAOM.Features.CultureDoctrine.Doctrines;
using TAOM.Features.CultureDoctrine.Hooks.Behaviors;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CultureDoctrine.Hooks.Tactics;

/// <summary>
/// Applies one <see cref="FormationPlan"/> to one <see cref="Formation"/>: vanilla's own
/// sequence (<c>ResetBehaviorWeights</c>, <c>SetDefaultBehaviorWeights</c>, then one
/// <c>SetBehaviorWeight&lt;T&gt;</c> per row) with the parameter each behaviour exposes set the way
/// vanilla's tactics set it. The switch is the only place a <see cref="BehaviorKind"/> meets a
/// concrete engine type. A TAOM behaviour (<see cref="TaomBehaviorBase"/>) is not among the
/// twenty-four the engine registers at spawn (`TeamAIGeneral.cs:63-86`), so its row first adds it to the formation's
/// <c>FormationAI</c> the first time a plan names it (<see cref="Ensure{T}"/>); adding happens on
/// the same team-AI tick that reads the list, and never again for that formation. Runs inside
/// the owning tactic's try/catch; <c>SetBehaviorWeight&lt;T&gt;</c> throws <c>MBException</c> for
/// a behaviour the formation was never given, and a <see cref="BehaviorKind"/> with no case
/// throws too, so a new enum member cannot silently leave a behaviour at its default weight;
/// both land in the failed-tactic latch rather than a native unwind.
/// <c>DoctrineSwitchInvariantTests</c> pins the switch.
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
                case BehaviorKind.Defend: ai.SetBehaviorWeight<BehaviorDefend>(w).DefensePosition = PositionFor(plan.Role, owner); break;
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
                case BehaviorKind.BracedDefend:
                    Ensure(formation, f => new BehaviorBracedDefend(f), owner);
                    ai.SetBehaviorWeight<BehaviorBracedDefend>(w).DefensePosition = PositionFor(plan.Role, owner);
                    break;
                case BehaviorKind.BracedAdvance:
                    Ensure(formation, f => new BehaviorBracedAdvance(f), owner);
                    ai.SetBehaviorWeight<BehaviorBracedAdvance>(w);
                    break;
                case BehaviorKind.InfantrySkirmish:
                    Ensure(formation, f => new BehaviorInfantrySkirmish(f), owner);
                    ai.SetBehaviorWeight<BehaviorInfantrySkirmish>(w);
                    break;
                case BehaviorKind.CycleCharge:
                    Ensure(formation, f => new BehaviorCycleCharge(f), owner);
                    ai.SetBehaviorWeight<BehaviorCycleCharge>(w);
                    break;
                case BehaviorKind.EnvelopWing:
                    Ensure(formation, f => new BehaviorEnvelopWing(f), owner);
                    ai.SetBehaviorWeight<BehaviorEnvelopWing>(w);
                    break;
                case BehaviorKind.FootCharge:
                    Ensure(formation, f => new BehaviorFootCharge(f), owner);
                    ai.SetBehaviorWeight<BehaviorFootCharge>(w);
                    DisarmVanillaCharge(ai);
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(plan), weights[i].Kind, "no behaviour type for this kind");
            }
        }
    }

    /// <summary>Vanilla's own weights for a formation the plan did not seat: reset, then Charge,
    /// PullBack, Stop and Reserve at 1 (`TacticComponent.cs:581-587`), so it fights as a
    /// vanilla tactic would have it instead of standing on stale weights.</summary>
    public static void ApplyDefaults(Formation formation)
    {
        formation.AI.ResetBehaviorWeights();
        TacticComponent.SetDefaultBehaviorWeights(formation);
    }

    /// <summary><c>SetDefaultBehaviorWeights</c> arms <c>BehaviorCharge</c> at 1 on every apply
    /// (`TacticComponent.cs:581-587`), and it charges <c>CachedClosestEnemyFormation</c> of any
    /// class; on a row that carries <c>FootCharge</c> it is the eored chase coming back through
    /// the default row, so the row disarms it. <c>DoctrineSwitchInvariantTests</c> pins this.</summary>
    private static void DisarmVanillaCharge(FormationAI ai) => ai.SetBehaviorWeight<BehaviorCharge>(0f);

    /// <summary>Adds the TAOM behaviour to the formation once and hands it the owning tactic's
    /// engagement distances. <c>GetBehavior&lt;T&gt;</c> is a linear scan of the formation's list
    /// (24 entries plus ours), run once per row per apply. A full scene reset (<c>Team.Reset</c>,
    /// a Custom Battle restart) rebuilds every <c>FormationAI</c> and drops the instance with its
    /// stage; the next apply adds a fresh one.</summary>
    public static T Ensure<T>(Formation formation, Func<Formation, T> create, TaomTacticBase owner) where T : TaomBehaviorBase
    {
        var behavior = Ensure(formation, create);
        behavior.Engagement = owner.Engagement;
        return behavior;
    }

    public static T Ensure<T>(Formation formation, Func<Formation, T> create) where T : TaomBehaviorBase
    {
        var existing = formation.AI.GetBehavior<T>();
        if (existing != null)
            return existing;
        var behavior = create(formation);
        formation.AI.AddAiBehavior(behavior);
        return behavior;
    }

    // The front line and the second line stand in different places; every other role gets the
    // plan's one defence position.
    private static WorldPosition PositionFor(FormationRole role, TaomTacticBase owner) =>
        role == FormationRole.SecondInfantry ? owner.SecondLinePosition : owner.DefensePosition;
}
