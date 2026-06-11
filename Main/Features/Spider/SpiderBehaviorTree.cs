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
/// Tree SHAPE is the elephant's (has-rider / ai-controlled / no-rider branches over the reused base
/// decorators); bite PACING is the warg's (ONE attack kind ⇒ a post-bite SleepTask is the cooldown —
/// no blackboard stamp state, per the simplicity criterion). The rider's normal cavalry AI drives all
/// movement; this tree only layers the bite on top. The anti-chain gate lives inside
/// <see cref="SpiderCanBiteDecorator"/>.
/// </summary>
public class SpiderBehaviorTree : BehaviorTree, IBTBannerlordBase, IBTSpiderBlackboard
{
    public BTBlackboardValue<Agent> Agent { get; set; }

    // base(10): NOT a 10ms throttle — BehaviorTreeAgentComponent.OnTick divides by 1000 in INT math, so any
    // value <1000 truncates to 0 and the tree runs every component tick (warg/elephant parity). Pacing
    // comes from the SleepTask leaves below; don't tune cadence via this ctor arg (elephant review 2026-06-10).
    public SpiderBehaviorTree(Agent agent) : base(10)
    {
        Agent = new BTBlackboardValue<Agent>(agent);
    }

    public static new BehaviorTree BuildTree(object[] objects)
    {
        if (objects[0] is not Agent agent) return null;

        return StartBuildingTree(new SpiderBehaviorTree(agent))
            .AddSelector("main")
                .AddSelector("has rider", new HasRiderDecorator())
                    .AddSelector("ai controlled", new IsAiControlledDecorator())
                        .AddSequence("bite", new SpiderCanBiteDecorator())
                            .AddTask(new SpiderAttackTask())
                            .AddTask(new SleepTask(new System.TimeSpan(0, 0, SpiderConfig.SleepAfterAttack)))
                        .Up()
                        .AddTask(new SleepTask(new System.TimeSpan(0, 0, 0, 0, 200)))  // idle: bounds the scan cadence
                    .Up()
                    .AddTask(new SleepTask(new System.TimeSpan(0, 0, 1)))              // player-ridden: ai branch skipped
                .Up()
                .AddSequence("no rider", new HasNoRiderDecorator())
                    .AddTask(new SleepTask(new System.TimeSpan(0, 0, 4)))              // riderless: rider died
                .Up()
            .Up()
            .AddConstantEventListener(new OnSpiderDied())
            .Finish();
    }
}
