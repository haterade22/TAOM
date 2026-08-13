using System;
using BehaviorTrees;
using BehaviorTreeWrapper.BlackBoardClasses;
using BehaviorTreeWrapper.Tasks;
using TAOM.Features.AdvancedCombat.BaseBehaviorTree;
using TAOM.Features.ElephantLike;
using TAOM.Features.ElephantLike.BehaviorTreeElements;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Elephant;

/// <summary>
/// Behavior tree for the AI war-elephant — the warg's per-agent BT pattern (see <c>WargBehaviorTree</c>) applied
/// to the elephant, built from the SHARED elephant-like nodes bound to <see cref="ElephantCombat.Profile"/>.
/// Built per elephant by <see cref="ElephantMissionBehavior"/> via a <c>BehaviorTreeAgentComponent</c>; the
/// engine auto-ticks it each frame.
///
/// Attack model (2026-06-10, replaces ADOD_Beasts's random per-tick roll): when a live enemy is in front of the tusks,
/// the elephant fires the TRAMPLE if off cooldown (10s, priority); otherwise a LEFT/RIGHT tusk swing picked by
/// the enemy's bearing if the side-attack cooldown (4s) allows; otherwise the tree idles and the engine's regular
/// mount AI (rider cavalry AI + native charge) simply continues — the BT only layers attacks on top of it.
/// The "already mid-attack-animation" gate inside <see cref="ElephantLikeEngageDecorator"/> prevents chaining
/// a swing into a playing trample.
///
/// Blackboard: cooldown stamps + target bearing (<see cref="IBTElephantLikeBlackboard"/>) shared across the nodes;
/// the rider is read off <c>Agent.RiderAgent</c> in the leaves, as the warg does. Phase 2 (player-triggered
/// trample, enrage/charge) slots in as new branches under "has rider" without disturbing these.
/// </summary>
public class ElephantBehaviorTree : BehaviorTree, IBTBannerlordBase, IBTElephantLikeBlackboard
{
    public BTBlackboardValue<Agent> Agent { get; set; }
    public BTBlackboardValue<DateTime?> TrampleLastFired { get; set; }
    public BTBlackboardValue<DateTime?> SideAttackLastFired { get; set; }
    public BTBlackboardValue<float> TargetBearing { get; set; }

    // base(10): NOT a 10ms throttle — BehaviorTreeAgentComponent.OnTick divides by 1000 in INT math, so any
    // value <1000 truncates to 0 and the tree runs every component tick (warg parity, same base(10)). Pacing
    // comes from the SleepTask leaves below; don't tune cadence via this ctor arg (review finding 2026-06-10).
    public ElephantBehaviorTree(Agent agent) : base(10)
    {
        Agent = new BTBlackboardValue<Agent>(agent);
        TrampleLastFired = new BTBlackboardValue<DateTime?>(null);
        SideAttackLastFired = new BTBlackboardValue<DateTime?>(null);
        TargetBearing = new BTBlackboardValue<float>(0f);
    }

    public static new BehaviorTree BuildTree(object[] objects)
    {
        if (objects[0] is not Agent agent) return null;
        var profile = ElephantCombat.Profile;
        return StartBuildingTree(new ElephantBehaviorTree(agent))
            .AddSelector("main")
                .AddSelector("has rider", new HasRiderDecorator())
                    .AddSelector("ai controlled", new IsAiControlledDecorator())
                        .AddSelector("enemy in range", new ElephantLikeEngageDecorator(profile))
                            .AddSequence("trample", new ElephantLikeAttackOffCooldownDecorator(profile, ElephantLikeAttackKind.Trample, ElephantConfig.TrampleCooldownSeconds))
                                .AddTask(new ElephantLikeTrampleTask(profile))
                                .AddTask(new SleepTask(new(0, 0, 0, 0, 300)))     // settle before next eval
                            .Up()
                            .AddSequence("side attack", new ElephantLikeAttackOffCooldownDecorator(profile, ElephantLikeAttackKind.SideAttack, ElephantConfig.SideAttackCooldownSeconds))
                                .AddTask(new ElephantLikeSideAttackTask(profile))
                                .AddTask(new SleepTask(new(0, 0, 0, 0, 300)))
                            .Up()
                        .Up()                                                     // both on cooldown → falls through
                        .AddTask(new SleepTask(new(0, 0, 0, 0, 200)))             // idle: bounds the scan cadence (~5/s)
                    .Up()
                    .AddTask(new SleepTask(new(0, 0, 1)))                         // player-ridden: ai branch skipped
                .Up()
                .AddSequence("no rider", new HasNoRiderDecorator())
                    .AddTask(new SleepTask(new(0, 0, 4)))                         // riderless: long idle
                .Up()
            .Finish();
    }
}
