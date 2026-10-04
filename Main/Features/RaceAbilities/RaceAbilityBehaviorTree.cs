using BehaviorTrees;
using BehaviorTreeWrapper.BlackBoardClasses;
using TAOM.Features.AdvancedCombat.BaseBehaviorTree;
using TAOM.Features.RaceAbilities.BehaviorTreeElements;
using TAOM.Features.RaceAbilities.Hooks;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.RaceAbilities;

/// <summary>
/// One tree per soldier with an ability profile (his race's, or for a man his culture's), the same shape
/// for every profile: the profile in race_abilities.json is what makes a dwarf brace and a berserker rage.
/// Built by RaceAbilitiesMissionLogic and ticked from <c>BehaviorTreeMissionLogic.OnMissionTick</c> on the
/// main thread (#592). base(1000) runs one full pass a second (BehaviorTreeAgentComponent divides the delay
/// in whole seconds, and staggers each tree's first pass across that second); its one leaf finishes on the
/// pass it starts, so each pass is one decision. A player-controlled soldier never fires.
/// </summary>
public class RaceAbilityBehaviorTree : BehaviorTree, IBTBannerlordBase
{
    public BTBlackboardValue<Agent> Agent { get; set; }

    public RaceAbilityBehaviorTree(Agent agent) : base(1000) => Agent = new BTBlackboardValue<Agent>(agent);

    public static new BehaviorTree? BuildTree(object[] objects)
    {
        if (objects[0] is not Agent agent)
            return null;
        // Resolved once per tree and passed to the node, so it is no service locator. Resolved here, not in
        // the BTRegister lambda: the registry keeps the first factory for the whole process, so a captured
        // service would outlive its container (WargBehaviorTree).
        var runtime = IoC.Resolve<RaceAbilityRuntime>();
        return StartBuildingTree(new RaceAbilityBehaviorTree(agent))
            .AddSelector("main")
                .AddSequence("ai soldier", new IsAiAgentDecorator())
                    .AddTask(new RaceAbilityTask(runtime))
                .Up()
                .AddTask(new ReturnTrueTask())          // player-controlled
            .Finish();
    }
}
