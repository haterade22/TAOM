using BehaviorTrees;
using BehaviorTreeWrapper;
using BehaviorTreeWrapper.AbstractDecoratorsListeners;
using System;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Warg.BehaviorTreeElements;

public class WargEnemyDiedDecorator : BannerlordEventDecorator, IBTWargBlackboard
{
    BTBlackboardValue<DateTime?> _rageAttackStartTime;
    BTBlackboardValue<Agent> _agentHitBy;
    BTBlackboardValue<int> _rageAttackAmount;
    BTBlackboardValue<bool> _firstAttack;
    public BTBlackboardValue<Agent> AgentHitBy { get => _agentHitBy; set => _agentHitBy = value; }
    public BTBlackboardValue<DateTime?> RageAttackStartTime { get => _rageAttackStartTime; set => _rageAttackStartTime = value; }
    public BTBlackboardValue<int> RageAttackAmount { get => _rageAttackAmount; set => _rageAttackAmount = value; }
    public BTBlackboardValue<bool> FirstAttack { get => _firstAttack; set => _firstAttack = value; }

    public WargEnemyDiedDecorator() : base(SubscriptionPossibilities.OnAgentRemoved) { }

    public override bool Evaluate()
    {
        if (AgentHitBy.GetValue() == null || AgentHitBy.GetValue().Health <= 0)
            AgentHitBy.SetValue(null);
        return AgentHitBy.GetValue() == null;
    }

    public override void Notify(object[] data)
    {
        Agent affectedAgent = data[0] as Agent;
        if (affectedAgent == AgentHitBy.GetValue())
            AgentHitBy.SetValue(null);
    }
}
