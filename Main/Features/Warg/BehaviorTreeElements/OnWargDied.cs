using BehaviorTrees;
using BehaviorTreeWrapper;
using BehaviorTreeWrapper.AbstractDecoratorsListeners;
using BehaviorTreeWrapper.BlackBoardClasses;
using TAOM.Features.AdvancedCombat.BaseBehaviorTree;
using System;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Warg.BehaviorTreeElements;

public class OnWargDied : BannerlordConstantEventListener, IBTBannerlordBase, IBTWargBlackboard, IBTInFormation, IBTMountBase
{
    BTBlackboardValue<Agent> _agent;
    BTBlackboardValue<Agent> _agentHitBy;
    BTBlackboardValue<int> _rageAttackAmount;
    BTBlackboardValue<DateTime?> _rageAttackStartTime;
    BTBlackboardValue<bool> _firstAttack;
    BTBlackboardValue<Formation> _agentsFormation;
    BTBlackboardValue<Agent> _rider;
    public BTBlackboardValue<Agent> Agent { get => _agent; set => _agent = value; }
    public BTBlackboardValue<Agent> AgentHitBy { get => _agentHitBy; set => _agentHitBy = value; }
    public BTBlackboardValue<int> RageAttackAmount { get => _rageAttackAmount; set => _rageAttackAmount = value; }
    public BTBlackboardValue<DateTime?> RageAttackStartTime { get => _rageAttackStartTime; set => _rageAttackStartTime = value; }
    public BTBlackboardValue<bool> FirstAttack { get => _firstAttack; set => _firstAttack = value; }
    public BTBlackboardValue<Formation> AgentsFormation { get => _agentsFormation; set => _agentsFormation = value; }
    public BTBlackboardValue<Agent> Rider { get => _rider; set => _rider = value; }

    public OnWargDied() : base(SubscriptionPossibilities.OnSelfRemoved) { }

    public override void Notify(object[] data)
    {
        // The stored rider's slot is checked there.
        Agent rider = CleanIfEnemyDied.ValidStoredRider(Rider.GetValue());
        if (rider == null || rider.Health == 0) return;

        // Only an open rage holds anything to give back: a warg that never raged still has its rider from the first scan.
        if (RageAttackStartTime.GetValue() != null) CleanIfEnemyDied.ResetAiControlledAgent(rider, AgentsFormation.GetValue());
        if (rider.IsPlayerControlled) FinishRageMode.Reset(Agent.GetValue());
    }
}
