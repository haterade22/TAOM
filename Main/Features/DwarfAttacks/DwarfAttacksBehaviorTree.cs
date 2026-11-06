using System;
using BehaviorTrees;
using BehaviorTreeWrapper.BlackBoardClasses;
using BehaviorTreeWrapper.Tasks;
using TAOM.Features.AdvancedCombat.BaseBehaviorTree;
using TAOM.Features.DwarfAttacks.BehaviorTreeElements;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.DwarfAttacks;

/// <summary>
/// Behavior tree for an AI dwarf on foot (#1087): the head butt and the drop kick. Built per dwarf by
/// <see cref="DwarfAttacksMissionBehavior"/> and run every mission tick by <c>Hooks.DwarfTreeHost</c> on the main
/// thread (#592), never as an agent component (the race-ability tree holds that slot). Between attacks the engine's own
/// melee AI fights as usual; a player-controlled dwarf only idles here.
/// </summary>
public class DwarfAttacksBehaviorTree : BehaviorTree, IBTBannerlordBase, IBTDwarfAttacksBlackboard
{
    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan PlayerIdle = TimeSpan.FromSeconds(2);

    public BTBlackboardValue<Agent> Agent { get; set; }
    public BTBlackboardValue<float?> HeadButtLastFired { get; set; }
    public BTBlackboardValue<float?> DropKickLastFired { get; set; }
    public BTBlackboardValue<DwarfAttack?> Chosen { get; set; }

    private DwarfAttacksBehaviorTree(Agent agent)
    {
        Agent = new BTBlackboardValue<Agent>(agent);
        HeadButtLastFired = new BTBlackboardValue<float?>(null);
        DropKickLastFired = new BTBlackboardValue<float?>(null);
        Chosen = new BTBlackboardValue<DwarfAttack?>(null);
    }

    /// <summary>The tree for one dwarf; <paramref name="ordinal"/> is his build order, which sets his scan period.</summary>
    internal static DwarfAttacksBehaviorTree Build(Agent agent, int ordinal, DwarfAttackService service,
        DwarfAttacksSettingsProvider settings, DwarfAttackChecks checks)
    {
        return StartBuildingTree(new DwarfAttacksBehaviorTree(agent))
            .AddSelector("main")
                .AddSelector("ai dwarf", new IsAiAgentDecorator())
                    .AddSequence("attack", new DwarfAttackReadyDecorator(service, settings))
                        .AddTask(new DwarfAttackTask(service, settings, checks))
                        .AddTask(new SleepTask(Settle))                          // settle before the next scan
                    .Up()
                    .AddTask(new SleepTask(service.IdleScanPeriod(ordinal)))     // idle: bounds the scan cadence
                .Up()
                .AddTask(new SleepTask(PlayerIdle))                              // player-controlled: long idle
            .Finish();
    }
}
