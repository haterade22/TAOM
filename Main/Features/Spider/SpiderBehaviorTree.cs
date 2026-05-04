using BehaviorTrees;
using BehaviorTreeWrapper;
using BehaviorTreeWrapper.BlackBoardClasses;
using BehaviorTreeWrapper.Tasks;
using TAOM.Features.AdvancedCombat.BaseBehaviorTree;
using TAOM.Features.Spider.BehaviorTreeElements;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Spider;

/// <summary>
/// Minimal AI tree for AI-controlled spider agents.
/// Selector: "no enemy near" → sleep ; otherwise → bite + sleep.
/// No rider logic, no rage mode (unlike Warg). Vanilla AI handles movement;
/// the BT only triggers attacks since vanilla AI doesn't drive non-humanoid strikes.
/// </summary>
public class SpiderBehaviorTree : BehaviorTree, IBTBannerlordBase, IBTSpiderBlackboard
{
    public BTBlackboardValue<Agent> Agent { get; set; }

    public SpiderBehaviorTree(Agent agent) : base(10)
    {
        Agent = new BTBlackboardValue<Agent>(agent);
    }

    public static new BehaviorTree BuildTree(object[] objects)
    {
        if (objects[0] is not Agent agent) return null;

        BehaviorTree tree = StartBuildingTree(new SpiderBehaviorTree(agent))
            .AddSelector("main")
                .AddSelector("no enemy near", new NoEnemyNearSpiderDecorator())
                    .AddTask(new SleepTask(new System.TimeSpan(0, 0, 1)))
                .Up()
                .AddSequence("bite enemy")
                    .AddTask(new SpiderAttackTask())
                    .AddTask(new SleepTask(new System.TimeSpan(0, 0, SpiderConfig.SleepAfterAttack)))
                .Up()
            .Up()
            .AddConstantEventListener(new OnSpiderDied())
            .Finish();

        return tree;
    }
}
