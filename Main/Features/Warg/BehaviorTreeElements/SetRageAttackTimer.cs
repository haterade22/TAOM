using BehaviorTrees;
using BehaviorTrees.Nodes;
using System;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Warg.BehaviorTreeElements;

public class SetRageAttackTimer : BTTask, IBTWargBlackboard
{
    BTBlackboardValue<DateTime?> _rageAttackStartTime;
    BTBlackboardValue<Agent> _agentHitBy;
    BTBlackboardValue<int> _rageAttackAmount;
    BTBlackboardValue<bool> _firstAttack;
    public BTBlackboardValue<Agent> AgentHitBy { get => _agentHitBy; set => _agentHitBy = value; }
    public BTBlackboardValue<DateTime?> RageAttackStartTime { get => _rageAttackStartTime; set => _rageAttackStartTime = value; }
    public BTBlackboardValue<int> RageAttackAmount { get => _rageAttackAmount; set => _rageAttackAmount = value; }
    public BTBlackboardValue<bool> FirstAttack { get => _firstAttack; set => _firstAttack = value; }

    public override BTTaskStatus Execute()
    {
        RageAttackStartTime.SetValue(DateTime.Now);
        return BTTaskStatus.FinishedWithTrue;
    }
}

public class ResetRageAttackTimer : BTTask, IBTWargBlackboard
{
    BTBlackboardValue<DateTime?> _rageAttackStartTime;
    BTBlackboardValue<Agent> _agentHitBy;
    BTBlackboardValue<int> _rageAttackAmount;
    BTBlackboardValue<bool> _firstAttack;
    public BTBlackboardValue<Agent> AgentHitBy { get => _agentHitBy; set => _agentHitBy = value; }
    public BTBlackboardValue<DateTime?> RageAttackStartTime { get => _rageAttackStartTime; set => _rageAttackStartTime = value; }
    public BTBlackboardValue<int> RageAttackAmount { get => _rageAttackAmount; set => _rageAttackAmount = value; }
    public BTBlackboardValue<bool> FirstAttack { get => _firstAttack; set => _firstAttack = value; }

    public override BTTaskStatus Execute()
    {
        RageAttackStartTime.SetValue(null);
        return BTTaskStatus.FinishedWithTrue;
    }
}
