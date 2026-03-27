using BehaviorTrees;
using BehaviorTreeWrapper.BlackBoardClasses;
using TAOM.Adapters;
using System;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Warg.BehaviorTreeElements;

public class WargAiControlledIsNotFacingEnemy : BTReturnFalseDecorator, IBTBannerlordBase, IBTWargBlackboard
{
    BTBlackboardValue<Agent> agent;
    BTBlackboardValue<Agent> _agentHitBy;
    BTBlackboardValue<int> _rageAttackAmount;
    BTBlackboardValue<DateTime?> _rageAttackStartTime;
    BTBlackboardValue<bool> _firstAttack;
    private static IMissionAdapterFactory AdapterFactory => IoC.Resolve<IMissionAdapterFactory>();

    public BTBlackboardValue<Agent> Agent { get => agent; set => agent = value; }
    public BTBlackboardValue<Agent> AgentHitBy { get => _agentHitBy; set => _agentHitBy = value; }
    public BTBlackboardValue<int> RageAttackAmount { get => _rageAttackAmount; set => _rageAttackAmount = value; }
    public BTBlackboardValue<DateTime?> RageAttackStartTime { get => _rageAttackStartTime; set => _rageAttackStartTime = value; }
    public BTBlackboardValue<bool> FirstAttack { get => _firstAttack; set => _firstAttack = value; }

    public override bool Evaluate()
    {
        var agentHitByAdapter = AdapterFactory.GetAgentAdapter(AgentHitBy.GetValue());
        var agentAdapter = AdapterFactory.GetAgentAdapter(Agent.GetValue());
        return !agentHitByAdapter.IsAttackLikelyToHit(agentAdapter, 30, WargConfig.WargAttackRange);
    }
}
