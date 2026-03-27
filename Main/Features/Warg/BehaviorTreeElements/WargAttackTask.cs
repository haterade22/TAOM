using BehaviorTrees;
using BehaviorTrees.Nodes;
using BehaviorTreeWrapper.BlackBoardClasses;
using System;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Warg.BehaviorTreeElements;

public class WargAttackTask : BTTask, IBTBannerlordBase, IBTWargBlackboard
{
    BTBlackboardValue<Agent> agent;
    BTBlackboardValue<Agent> _agentHitBy;
    BTBlackboardValue<int> _rageAttackAmount;
    BTBlackboardValue<DateTime?> _rageAttackStartTime;
    BTBlackboardValue<bool> _firstAttack;
    public BTBlackboardValue<Agent> Agent { get => agent; set => agent = value; }
    public BTBlackboardValue<Agent> AgentHitBy { get => _agentHitBy; set => _agentHitBy = value; }
    public BTBlackboardValue<int> RageAttackAmount { get => _rageAttackAmount; set => _rageAttackAmount = value; }
    public BTBlackboardValue<DateTime?> RageAttackStartTime { get => _rageAttackStartTime; set => _rageAttackStartTime = value; }
    public BTBlackboardValue<bool> FirstAttack { get => _firstAttack; set => _firstAttack = value; }

    public override BTTaskStatus Execute()
    {
        RageAttackAmount.SetValue(RageAttackAmount.GetValue() - 1);
        Agent warg = Agent.GetValue();
        IoC.Resolve<IWargAttackService>().WargAttack(warg);
        return BTTaskStatus.FinishedWithTrue;
    }
}
