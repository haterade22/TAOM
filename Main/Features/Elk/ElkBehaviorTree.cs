using System;
using BehaviorTrees;
using BehaviorTreeWrapper.BlackBoardClasses;
using BehaviorTreeWrapper.Tasks;
using TAOM.Features.AdvancedCombat.BaseBehaviorTree;
using TAOM.Features.ElephantLike;
using TAOM.Features.ElephantLike.BehaviorTreeElements;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Elk;

/// <summary>
/// Behavior tree for the ridden great elk: the antler charge. The war ram's tree shape
/// (<see cref="TAOM.Features.WarRam.WarRamBehaviorTree"/>), built from the SHARED elephant-like nodes bound to
/// <see cref="ElkCombat.Profile"/>. Built per elk by <see cref="ElkMissionBehavior"/> via a
/// <c>BehaviorTreeAgentComponent</c> and ticked from <c>BehaviorTreeMissionLogic.OnMissionTick</c> on the main
/// thread (#592).
///
/// One branch: when a live enemy is in front within <see cref="ElkConfig.AttackTriggerRange"/> and the charge is
/// off cooldown (10 s), the elk lowers its antlers (act_war_ram_butt, the ram's head-down clip on the shared
/// horse_skeleton) and lands one 60 Blunt blow, scaled by the rider's career charge bonus, on the one enemy it faces
/// most squarely; the blow is the rider's. It fires under ANY rider, the player included, as the warg's bite does
/// (Mike, 2026-09-23): the attack is automatic and the rider keeps steering. Between charges it idles and the engine's
/// own mount handling (the rider's steering or cavalry AI, plus native charge collision) carries on underneath.
///
/// Blackboard: the cooldown stamps and target bearing (<see cref="IBTElephantLikeBlackboard"/>) exist because the
/// interface requires them; <c>SideAttackLastFired</c> and <c>TargetBearing</c> are never READ here, as on the ram.
/// </summary>
public class ElkBehaviorTree : BehaviorTree, IBTBannerlordBase, IBTElephantLikeBlackboard
{
    public BTBlackboardValue<Agent> Agent { get; set; }
    public BTBlackboardValue<DateTime?> TrampleLastFired { get; set; }
    public BTBlackboardValue<DateTime?> SideAttackLastFired { get; set; }
    public BTBlackboardValue<float> TargetBearing { get; set; }

    // base(10): NOT a 10ms throttle. BehaviorTreeAgentComponent divides by 1000 in INT math, so any value <1000
    // truncates to 0 and the tree runs every tick (warg/elephant/mumakil/ram parity). Pacing comes from the
    // SleepTask leaves below; don't tune cadence via this ctor arg.
    public ElkBehaviorTree(Agent agent) : base(10)
    {
        Agent = new BTBlackboardValue<Agent>(agent);
        TrampleLastFired = new BTBlackboardValue<DateTime?>(null);
        SideAttackLastFired = new BTBlackboardValue<DateTime?>(null);
        TargetBearing = new BTBlackboardValue<float>(0f);
    }

    public static new BehaviorTree BuildTree(object[] objects)
    {
        if (objects[0] is not Agent agent) return null;
        var profile = ElkCombat.Profile;
        return StartBuildingTree(new ElkBehaviorTree(agent))
            .AddSelector("main")
                .AddSelector("has rider", new HasRiderDecorator())                // AI or player rider alike (warg parity)
                    .AddSelector("enemy in range", new ElephantLikeEngageDecorator(profile))
                        .AddSequence("antler charge", new ElephantLikeAttackOffCooldownDecorator(profile, ElephantLikeAttackKind.Trample, ElkConfig.AttackCooldownSeconds))
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
