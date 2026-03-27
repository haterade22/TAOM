using BehaviorTrees;
using BehaviorTreeWrapper.BlackBoardClasses;
using BehaviorTreeWrapper.Tasks;
using TAOM.Features.AdvancedCombat.BaseBehaviorTree;
using TAOM.Features.Warg.BehaviorTreeElements;
using System;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Warg;

public class WargBehaviorTree : BehaviorTree, IBTBannerlordBase, IBTWargBlackboard, IBTInFormation, IBTMountBase
{
    public BTBlackboardValue<Agent> Agent { get; set; }
    public BTBlackboardValue<Agent> AgentHitBy { get; set; }
    public BTBlackboardValue<int> RageAttackAmount { get; set; }
    public BTBlackboardValue<DateTime?> RageAttackStartTime { get; set; }
    public BTBlackboardValue<bool> FirstAttack { get; set; }
    public BTBlackboardValue<Formation> AgentsFormation { get; set; }
    public WargTryToGoRage RageDecorator { get; set; }
    public BTBlackboardValue<Agent> Rider { get; set; }

    public WargBehaviorTree(Agent agent) : base(10)
    {
        Agent = new BTBlackboardValue<Agent>(agent);
        RageAttackAmount = new BTBlackboardValue<int>(0);
        RageAttackStartTime = new BTBlackboardValue<DateTime?>(null);
        AgentHitBy = new BTBlackboardValue<Agent>(null);
        FirstAttack = new BTBlackboardValue<bool>(false);
        Rider = new BTBlackboardValue<Agent>(agent.RiderAgent);
        AgentsFormation = new BTBlackboardValue<Formation>(agent.RiderAgent?.Formation);
    }

    public static new BehaviorTree BuildTree(object[] objects)
    {
        if (objects[0] is not Agent agent) return null;
        BehaviorTree tree = StartBuildingTree(new WargBehaviorTree(agent))
            .AddSelector("main")
            .AddSelector("has rider", new HasRiderDecorator())
            .AddSelector("no enemy close", new NoEnemyCloseDecorator())
            .AddTask(new SleepTask(new(0, 0, 1)))
            .Up()
            .AddSelector("enter rage mode", new WargHitByEnemyDecorator())
            .AddSequence("player controlled", new IsPlayerControlledDecorator())
            .AddTask(new PrepareOnFirstCall())
            .AddTask(new SetRageAttackTimer())
            .AddSelector("rage player logic")
            .AddSequence("enemy died", new WargEnemyDiedDecorator())
            .AddTask(new LogTask("enemy died, rage mode finished"))
            .Up()
            .AddSequence("find enemy", new PeriodicallyCheckIfCanAttackAnyone())
            .AddTask(new LogTask("found enemy"))
            .AddTask(new WargAttackTask())
            .AddTask(new SleepTask(new(0, 0, 1)))
            .Up()
            .AddSequence("can not find enemy", new WargCanNotFindEnemyDecorator(6))
            .AddTask(new LogTask("can not get to enemy"))
            .AddTask(new FinishRageMode())
            .Up()

            .Up()
            .AddTask(new CleanIfEnemyDied())
            .Up()
            .AddSequence("non player controlled", new IsAiControlledDecorator())
            .AddTask(new PrepareOnFirstCall())
            .AddTask(new SetRageAttackTimer())
            .AddSelector("rage ai logic")
            .AddSequence("enemy died", new WargEnemyDiedDecorator())
            .AddTask(new ReturnTrueTask())
            .Up()
            .AddSequence("can not find enemy", new WargCanNotFindEnemyDecorator(6))
            .AddTask(new FinishRageMode())
            .Up()
            .AddSequence("able to attack enemy", new PeriodicallyCheckIfCanAttackAnyone())
            .AddTask(new WargAttackTask())
            .AddTask(new SleepTask(new(0, 0, 1)))
            .Up()
            .AddSequence("get to the enemy", new WargAiControlledIsNotFacingEnemy())
            .AddTask(new WargAiControlledGetToEnemy())
            .AddTask(new SleepTask(new(0, 0, 0, 0, 250)))
            .Up()

            .Up()
            .AddTask(new CleanIfEnemyDied())
            .Up()
            .AddTask(new ResetRageAttackTimer())
            .Up()
            .AddSequence("hit enemy", new CheckOnceIfCanAttackEnemy())
            .AddTask(new WargAttackTask())
            .AddTask(new SleepTask(new(0, 0, WargConfig.SleepAfterAttack)))
            .Up()
            .Up()
            .AddSequence("no rider", new HasNoRiderDecorator())
            .AddTask(new SleepTask(new(0, 0, 4)))
            .Up()
            .Up()
            .AddConstantEventListener(new WargTryToGoRage())
            .AddConstantEventListener(new OnWargDied())
            .Finish();
        return tree;
    }
}
