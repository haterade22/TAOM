using BehaviorTrees;
using BehaviorTreeWrapper.BlackBoardClasses;
using BehaviorTreeWrapper.Tasks;
using TAOM.Features.AdvancedCombat.BaseBehaviorTree;
using TAOM.Features.TrollBruteForce.BehaviorTreeElements;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.TrollBruteForce;

/// <summary>
/// Behavior tree for an AI troll on foot, cave or hill (#649): the Brute Force smash. Built per troll by
/// <see cref="TrollBruteForceMissionBehavior"/> and ticked from <c>BehaviorTreeMissionLogic.OnMissionTick</c> on the
/// main thread (#592). The first tree in TAOM on an agent with no rider, so it uses <see cref="IsAiAgentDecorator"/>,
/// not the rider-based gates. Between smashes the engine's own melee AI fights as usual; a player-controlled troll
/// only idles here.
/// </summary>
public class TrollBruteForceBehaviorTree : BehaviorTree, IBTBannerlordBase, IBTTrollBruteForceBlackboard
{
    public BTBlackboardValue<Agent> Agent { get; set; }
    public BTBlackboardValue<float?> LastFired { get; set; }

    // base(10) runs the tree every tick (BehaviorTreeAgentComponent's int division, see ElkBehaviorTree);
    // pacing comes from the SleepTask leaves.
    public TrollBruteForceBehaviorTree(Agent agent) : base(10)
    {
        Agent = new BTBlackboardValue<Agent>(agent);
        LastFired = new BTBlackboardValue<float?>(null);
    }

    public static new BehaviorTree BuildTree(object[] objects)
    {
        if (objects[0] is not Agent agent) return null;
        return StartBuildingTree(new TrollBruteForceBehaviorTree(agent))
            .AddSelector("main")
                .AddSelector("ai troll", new IsAiAgentDecorator())
                    .AddSequence("brute force", new BruteForceReadyDecorator())
                        .AddTask(new BruteForceTask())
                        .AddTask(new SleepTask(new(0, 0, 0, 0, 300)))       // settle before the next scan
                    .Up()
                    .AddTask(new SleepTask(new(0, 0, 0, 0, 250)))           // idle: bounds the scan cadence
                .Up()
                .AddTask(new SleepTask(new(0, 0, 2)))                       // player-controlled: long idle
            .Finish();
    }
}
