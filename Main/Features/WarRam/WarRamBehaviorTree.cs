using System;
using BehaviorTrees;
using BehaviorTreeWrapper.BlackBoardClasses;
using BehaviorTreeWrapper.Tasks;
using TAOM.Features.AdvancedCombat.BaseBehaviorTree;
using TAOM.Features.ElephantLike;
using TAOM.Features.ElephantLike.BehaviorTreeElements;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.WarRam;

/// <summary>
/// Behavior tree for the AI war ram, the warg/elephant/Mumakil per-agent BT pattern, built from the
/// SHARED elephant-like nodes bound to <see cref="WarRamCombat.Profile"/>. Built per ram by
/// <see cref="WarRamMissionBehavior"/> via a <c>BehaviorTreeAgentComponent</c>; the engine auto-ticks it
/// each frame.
///
/// Attack model: ONLY the kick-attack branch is wired, deliberately (see <see cref="WarRamConfig"/>).
/// When a live enemy is in front and the kick is off cooldown (6s), the ram fires it via
/// <see cref="ElephantLikeTrampleTask"/>; there is no side-attack fallback sequence like the war
/// elephant/Mumakil's tusk swing, so the ram simply idles between kicks and the engine's regular mount
/// AI (rider cavalry AI + native charge) continues underneath, same as the other elephant-like
/// creatures. This is a single-attack creature by design, not a scaled-down elephant.
///
/// Blackboard: cooldown stamps + target bearing (<see cref="IBTElephantLikeBlackboard"/>) are declared
/// on this tree because the interface requires them, even though <c>SideAttackLastFired</c> and
/// <c>TargetBearing</c> are never READ here (there is no side-attack node to read them).
/// <see cref="ElephantLikeEngageDecorator"/> still writes <c>TargetBearing</c> on every passing scan; it
/// is simply unconsumed, matching the shared node's contract without needing a ram-specific variant.
/// </summary>
public class WarRamBehaviorTree : BehaviorTree, IBTBannerlordBase, IBTElephantLikeBlackboard
{
    public BTBlackboardValue<Agent> Agent { get; set; }
    public BTBlackboardValue<DateTime?> TrampleLastFired { get; set; }
    public BTBlackboardValue<DateTime?> SideAttackLastFired { get; set; }
    public BTBlackboardValue<float> TargetBearing { get; set; }

    // base(10): NOT a 10ms throttle - BehaviorTreeAgentComponent.OnTick divides by 1000 in INT math, so
    // any value <1000 truncates to 0 and the tree runs every component tick (elephant/warg/mumakil
    // parity, same base(10)). Pacing comes from the SleepTask leaves below; don't tune cadence via this
    // ctor arg.
    public WarRamBehaviorTree(Agent agent) : base(10)
    {
        Agent = new BTBlackboardValue<Agent>(agent);
        TrampleLastFired = new BTBlackboardValue<DateTime?>(null);
        SideAttackLastFired = new BTBlackboardValue<DateTime?>(null);
        TargetBearing = new BTBlackboardValue<float>(0f);
    }

    public static new BehaviorTree BuildTree(object[] objects)
    {
        if (objects[0] is not Agent agent) return null;
        var profile = WarRamCombat.Profile;
        return StartBuildingTree(new WarRamBehaviorTree(agent))
            .AddSelector("main")
                .AddSelector("has rider", new HasRiderDecorator())
                    .AddSelector("ai controlled", new IsAiControlledDecorator())
                        .AddSelector("enemy in range", new ElephantLikeEngageDecorator(profile))
                            .AddSequence("kick attack", new ElephantLikeAttackOffCooldownDecorator(profile, ElephantLikeAttackKind.Trample, WarRamConfig.AttackCooldownSeconds))
                                .AddTask(new ElephantLikeTrampleTask(profile))
                                .AddTask(new SleepTask(new(0, 0, 0, 0, 300)))     // settle before next eval
                            .Up()
                        .Up()                                                     // on cooldown -> falls through
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
