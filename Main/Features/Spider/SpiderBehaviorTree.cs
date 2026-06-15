using System;
using BehaviorTrees;
using BehaviorTreeWrapper.BlackBoardClasses;
using BehaviorTreeWrapper.Tasks;
using TAOM.Features.AdvancedCombat.BaseBehaviorTree;
using TAOM.Features.Spider.BehaviorTreeElements;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Spider;

/// <summary>
/// Behavior tree for the RIDDEN giant spider — the third creature in the warg → elephant BT lineage.
/// Built per spider-mount agent by <see cref="SpiderMissionBehavior"/> via a
/// <c>BehaviorTreeAgentComponent</c>; the engine auto-ticks it each frame.
///
/// Attack model (2026-06-15) is the ELEPHANT's directional model: when a live enemy is engageable, the spider
/// fires the PRIORITY pounce (front bite, or the charge variant at speed) if off its long cooldown; otherwise a
/// LEFT/RIGHT swipe picked by the enemy's bearing if the side cooldown allows; otherwise the tree idles and the
/// rider's cavalry AI simply continues — the BT only layers attacks on top. The "already mid-attack" gate inside
/// <see cref="SpiderEngageDecorator"/> prevents chaining a swipe into a playing pounce. Each attack keeps the
/// spider's bone-collision damage (the warg/elephant share <c>CustomAttacksUtils.TakeDamage</c>, NaN-guarded).
///
/// Blackboard: cooldown stamps + target bearing (<see cref="IBTSpiderBlackboard"/>) shared across the nodes;
/// the rider is read off <c>Agent.RiderAgent</c> in the leaves, as the warg/elephant do.
/// </summary>
public class SpiderBehaviorTree : BehaviorTree, IBTBannerlordBase, IBTSpiderBlackboard
{
    public BTBlackboardValue<Agent> Agent { get; set; }
    public BTBlackboardValue<DateTime?> PounceLastFired { get; set; }
    public BTBlackboardValue<DateTime?> SideAttackLastFired { get; set; }
    public BTBlackboardValue<float> TargetBearing { get; set; }

    // base(10): NOT a 10ms throttle — BehaviorTreeAgentComponent.OnTick divides by 1000 in INT math, so any
    // value <1000 truncates to 0 and the tree runs every component tick (warg/elephant parity). Pacing comes from
    // the cooldown decorators + SleepTask leaves below; don't tune cadence via this ctor arg.
    public SpiderBehaviorTree(Agent agent) : base(10)
    {
        Agent = new BTBlackboardValue<Agent>(agent);
        PounceLastFired = new BTBlackboardValue<DateTime?>(null);
        SideAttackLastFired = new BTBlackboardValue<DateTime?>(null);
        TargetBearing = new BTBlackboardValue<float>(0f);
    }

    public static new BehaviorTree BuildTree(object[] objects)
    {
        if (objects[0] is not Agent agent) return null;

        return StartBuildingTree(new SpiderBehaviorTree(agent))
            .AddSelector("main")
                .AddSelector("has rider", new HasRiderDecorator())
                    .AddSelector("ai controlled", new IsAiControlledDecorator())
                        .AddSelector("engage", new SpiderEngageDecorator())
                            .AddSequence("pounce", new SpiderAttackOffCooldownDecorator(SpiderAttackKind.Pounce, SpiderConfig.PounceCooldownSeconds))
                                .AddTask(new SpiderPounceTask())
                                .AddTask(new SleepTask(new TimeSpan(0, 0, 0, 0, 300)))   // settle before next eval
                            .Up()
                            .AddSequence("side attack", new SpiderAttackOffCooldownDecorator(SpiderAttackKind.SideAttack, SpiderConfig.SideAttackCooldownSeconds))
                                .AddTask(new SpiderSideAttackTask())
                                .AddTask(new SleepTask(new TimeSpan(0, 0, 0, 0, 300)))
                            .Up()
                        .Up()                                                            // both on cooldown → falls through
                        .AddTask(new SleepTask(new TimeSpan(0, 0, 0, 0, 200)))           // idle: bounds the scan cadence (~5/s)
                    .Up()
                    .AddTask(new SleepTask(new TimeSpan(0, 0, 1)))                        // player-ridden: ai branch skipped
                .Up()
                .AddSequence("no rider", new HasNoRiderDecorator())
                    .AddTask(new SleepTask(new TimeSpan(0, 0, 4)))                        // riderless: rider died
                .Up()
            .Up()
            .AddConstantEventListener(new OnSpiderDied())
            .Finish();
    }
}
