using BehaviorTrees;
using BehaviorTreeWrapper;
using BehaviorTreeWrapper.AbstractDecoratorsListeners;
using BehaviorTreeWrapper.BlackBoardClasses;
using TAOM.Features.AdvancedCombat.BaseBehaviorTree;
using System;
using TaleWorlds.MountAndBlade;
using TAOM.Features.FordsOfWar;

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

    // Mike, 2026-11-07: no rage in a Fords of War match, where it is not needed. This listener is the only code that starts a rage,
    // and it is built with its tree when the warg joins the mission, so the mission is read once here.
    private readonly bool _rageAllowed = !FordsOfWarMission.IsActive(Mission.Current);

    public override void Notify(object[] data)
    {
        Agent affectorAgent = (Agent)data[0];
        Blow blow = (Blow)data[2];
        if (!_rageAllowed || RageAttackAmount.GetValue() > 0
            || blow.IsFallDamage
            || blow.IsMissile) return;
        if (blow.InflictedDamage > WargConfig.minDamageReceivedForRage
            && affectorAgent.Position.Distance(Agent.GetValue().Position) < WargConfig.maxDistanceFromWargToRollForRage
            && _random.NextDouble() <= WargConfig.rageChance)
        {
            RageAttackAmount.SetValue(WargConfig.rageAttacks);
            AgentHitBy.SetValue(affectorAgent);
            FirstAttack.SetValue(true);
        }
    }
}
