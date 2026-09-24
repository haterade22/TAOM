using BehaviorTrees;
using BehaviorTrees.Nodes;
using BehaviorTreeWrapper.BlackBoardClasses;
using System;
using TAOM.Adapters;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Warg.BehaviorTreeElements;

public class WargAttackTask : BTTask, IBTBannerlordBase, IBTWargBlackboard
{
    BTBlackboardValue<Agent> agent;
    BTBlackboardValue<Agent> _agentHitBy;
    BTBlackboardValue<int> _rageAttackAmount;
    BTBlackboardValue<DateTime?> _rageAttackStartTime;
    BTBlackboardValue<bool> _firstAttack;
    // Resolved once when the tree is built, not on every attack. IWargAttackService is registered
    // Transient, but WargAttackService keeps no per-call state, so one instance per task is equivalent.
    private readonly IMissionAdapterFactory _adapterFactory = IoC.Resolve<IMissionAdapterFactory>();
    private readonly IWargAttackService _attackService = IoC.Resolve<IWargAttackService>();
    public BTBlackboardValue<Agent> Agent { get => agent; set => agent = value; }
    public BTBlackboardValue<Agent> AgentHitBy { get => _agentHitBy; set => _agentHitBy = value; }
    public BTBlackboardValue<int> RageAttackAmount { get => _rageAttackAmount; set => _rageAttackAmount = value; }
    public BTBlackboardValue<DateTime?> RageAttackStartTime { get => _rageAttackStartTime; set => _rageAttackStartTime = value; }
    public BTBlackboardValue<bool> FirstAttack { get => _firstAttack; set => _firstAttack = value; }

    public override BTTaskStatus Execute()
    {
        RageAttackAmount.SetValue(RageAttackAmount.GetValue() - 1);
        Agent warg = Agent.GetValue();
        if (warg != null)
        {
            // Boundary: wrap sealed Agent into adapter before crossing into service (ADR-007).
            var wargAdapter = _adapterFactory.GetAgentAdapter(warg);
            _attackService.WargAttack(wargAdapter);
        }
        return BTTaskStatus.FinishedWithTrue;
    }
}
