using BehaviorTrees;
using BehaviorTreeWrapper;
using BehaviorTreeWrapper.AbstractDecoratorsListeners;
using BehaviorTreeWrapper.BlackBoardClasses;
using TAOM.Features.AdvancedCombat.BaseBehaviorTree;
using System;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Warg.BehaviorTreeElements;

public class WargTryToGoRage : BannerlordConstantEventListener, IBTBannerlordBase, IBTWargBlackboard, IBTMountBase
{
    BTBlackboardValue<Agent> _agent;
    BTBlackboardValue<Agent> _agentHitBy;
    BTBlackboardValue<int> _rageAttackAmount;
    BTBlackboardValue<DateTime?> _rageAttackStartTime;
    BTBlackboardValue<bool> _firstAttack;
    BTBlackboardValue<Agent> _rider;
    public BTBlackboardValue<Agent> Agent { get => _agent; set => _agent = value; }
    public BTBlackboardValue<Agent> AgentHitBy { get => _agentHitBy; set => _agentHitBy = value; }
    public BTBlackboardValue<int> RageAttackAmount { get => _rageAttackAmount; set => _rageAttackAmount = value; }
    public BTBlackboardValue<DateTime?> RageAttackStartTime { get => _rageAttackStartTime; set => _rageAttackStartTime = value; }
    public BTBlackboardValue<bool> FirstAttack { get => _firstAttack; set => _firstAttack = value; }
    public BTBlackboardValue<Agent> Rider { get => _rider; set => _rider = value; }

    public WargTryToGoRage() : base(SubscriptionPossibilities.OnSelfIsHit) { }

    Random _random = new();

    public override void Notify(object[] data)
    {
        Agent affectorAgent = (Agent)data[0];
        Blow blow = (Blow)data[2];
        if (RageAttackAmount.GetValue() > 0
            || blow.IsFallDamage
            || blow.IsMissile) return;
        if (blow.InflictedDamage > WargConfig.minDamageReceivedForRage
            && affectorAgent.Position.Distance(Agent.GetValue().Position) < WargConfig.maxDistanceFromWargToRollForRage
            && _random.NextDouble() <= WargConfig.rageChance)
        {
            int amountOfAttacks = _random.Next(WargConfig.minRageAttacks, WargConfig.maxRageAttacks);
            RageAttackAmount.SetValue(amountOfAttacks);
            AgentHitBy.SetValue(affectorAgent);
            FirstAttack.SetValue(true);
        }
    }
}
