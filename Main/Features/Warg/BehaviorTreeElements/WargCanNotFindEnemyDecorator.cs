using BehaviorTrees;
using BehaviorTreeWrapper.AbstractDecoratorsListeners;
using System;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Warg.BehaviorTreeElements;

public class WargCanNotFindEnemyDecorator : BannerlordTickTimedDecorator, IBTWargBlackboard
{
    private readonly TimeSpan waitSeconds;
    private BTBlackboardValue<DateTime?> _rageAttackStartTime;
    BTBlackboardValue<Agent> _agentHitBy;
    BTBlackboardValue<int> _rageAttackAmount;
    BTBlackboardValue<bool> _firstAttack;

    public BTBlackboardValue<Agent> AgentHitBy { get => _agentHitBy; set => _agentHitBy = value; }
    public BTBlackboardValue<int> RageAttackAmount { get => _rageAttackAmount; set => _rageAttackAmount = value; }
    public BTBlackboardValue<bool> FirstAttack { get => _firstAttack; set => _firstAttack = value; }
    public BTBlackboardValue<DateTime?> RageAttackStartTime { get => _rageAttackStartTime; set => _rageAttackStartTime = value; }

    public WargCanNotFindEnemyDecorator(double timeToWait) : base(timeToWait)
    {
        waitSeconds = TimeSpan.FromSeconds(timeToWait);
    }

    public override bool Evaluate()
    {
        if (RageAttackStartTime == null || RageAttackStartTime.GetValue() == null) return false;
        if (DateTime.Now - RageAttackStartTime.GetValue() > waitSeconds)
            return true;
        return false;
    }

    public override void Notify(object[] data) { }
}
