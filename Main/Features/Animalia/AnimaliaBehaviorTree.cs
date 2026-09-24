using System;
using BehaviorTrees;
using BehaviorTreeWrapper.BlackBoardClasses;
using BehaviorTreeWrapper.Tasks;
using TAOM.Features.AdvancedCombat.BaseBehaviorTree;
using TAOM.Features.ElephantLike;
using TAOM.Features.ElephantLike.BehaviorTreeElements;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Animalia;

/// <summary>
/// Behavior tree for a ridden Animalia elk or moose: its antler attack. The great elk's tree shape
/// (<see cref="TAOM.Features.Elk.ElkBehaviorTree"/>), built from the SHARED elephant-like nodes, with the animal's
/// profile passed in, so one class serves both animals (<see cref="AnimaliaMissionBehavior"/> registers one tree name
/// per animal). Ticked from <c>BehaviorTreeMissionLogic.OnMissionTick</c> on the main thread (#592).
///
/// One branch: a live enemy in front within the trigger range, the attack off cooldown, then the trample task plays
/// the animal's own attack clip and lands one blow on the enemy it faces most squarely; the blow is the rider's. It
/// fires under ANY rider, the player included (warg and great elk parity). Between attacks it idles and the engine's
/// own mount handling carries on underneath.
/// </summary>
public class AnimaliaBehaviorTree : BehaviorTree, IBTBannerlordBase, IBTElephantLikeBlackboard
{
    public BTBlackboardValue<Agent> Agent { get; set; }
    public BTBlackboardValue<DateTime?> TrampleLastFired { get; set; }
    public BTBlackboardValue<DateTime?> SideAttackLastFired { get; set; }
    public BTBlackboardValue<float> TargetBearing { get; set; }

    // base(10): NOT a 10ms throttle (BehaviorTreeAgentComponent divides by 1000 in int math, so it runs every tick);
    // pacing comes from the SleepTask leaves, as on every creature tree.
    public AnimaliaBehaviorTree(Agent agent) : base(10)
    {
        Agent = new BTBlackboardValue<Agent>(agent);
        TrampleLastFired = new BTBlackboardValue<DateTime?>(null);
        SideAttackLastFired = new BTBlackboardValue<DateTime?>(null);
        TargetBearing = new BTBlackboardValue<float>(0f);
    }

    public static BehaviorTree? Build(object[] objects, ElephantLikeCombatProfile profile)
    {
        if (objects[0] is not Agent agent) return null;
        return StartBuildingTree(new AnimaliaBehaviorTree(agent))
            .AddSelector("main")
                .AddSelector("has rider", new HasRiderDecorator())
                    .AddSelector("enemy in range", new ElephantLikeEngageDecorator(profile))
                        .AddSequence("antler attack", new ElephantLikeAttackOffCooldownDecorator(profile, ElephantLikeAttackKind.Trample, AnimaliaConfig.AttackCooldownSeconds))
                            .AddTask(new ElephantLikeTrampleTask(profile))
                            .AddTask(new SleepTask(new(0, 0, 0, 0, 300)))         // settle before next eval
                        .Up()
                    .Up()                                                         // on cooldown -> falls through
                    .AddTask(new SleepTask(new(0, 0, 0, 0, 200)))                 // idle: bounds the scan cadence (~5/s)
                .Up()
                .AddSequence("no rider", new HasNoRiderDecorator())
                    .AddTask(new SleepTask(new(0, 0, 4)))                         // riderless: long idle
                .Up()
            .Finish();
    }
}
