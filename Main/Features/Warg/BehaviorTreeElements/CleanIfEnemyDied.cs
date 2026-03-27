using BehaviorTrees;
using BehaviorTrees.Nodes;
using BehaviorTreeWrapper.BlackBoardClasses;
using TAOM.Features.AdvancedCombat.BaseBehaviorTree;
using System;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Warg.BehaviorTreeElements;

public class CleanIfEnemyDied : BTTask, IBTBannerlordBase, IBTWargBlackboard, IBTInFormation
{
    BTBlackboardValue<Agent> _agent;
    BTBlackboardValue<Agent> _agentHitBy;
    BTBlackboardValue<int> _rageAttackAmount;
    BTBlackboardValue<DateTime?> _rageAttackStartTime;
    BTBlackboardValue<bool> _firstAttack;
    BTBlackboardValue<Formation> _agentsFormation;
    public BTBlackboardValue<Agent> Agent { get => _agent; set => _agent = value; }
    public BTBlackboardValue<Agent> AgentHitBy { get => _agentHitBy; set => _agentHitBy = value; }
    public BTBlackboardValue<int> RageAttackAmount { get => _rageAttackAmount; set => _rageAttackAmount = value; }
    public BTBlackboardValue<DateTime?> RageAttackStartTime { get => _rageAttackStartTime; set => _rageAttackStartTime = value; }
    public BTBlackboardValue<bool> FirstAttack { get => _firstAttack; set => _firstAttack = value; }
    public BTBlackboardValue<Formation> AgentsFormation { get => _agentsFormation; set => _agentsFormation = value; }

    public override BTTaskStatus Execute()
    {
        if (AgentHitBy.GetValue() == null || AgentHitBy.GetValue().Health <= 0 || RageAttackAmount.GetValue() == 0)
        {
            RageAttackStartTime.SetValue(null);
            RageAttackAmount.SetValue(0);
            AgentHitBy.SetValue(null);

            if (Agent.GetValue().IsPlayerControlled)
                FinishRageMode.Reset(Agent.GetValue());
            else ResetAiControlledAgent(Agent.GetValue()?.RiderAgent, AgentsFormation.GetValue());
        }
        return BTTaskStatus.FinishedWithTrue;
    }

    public static void ResetAiControlledAgent(Agent rider, Formation formation)
    {
        if (rider == null || rider.State != TaleWorlds.Core.AgentState.Active) return;
        rider.Formation = formation;
        rider.DisableScriptedMovement();
    }
}
