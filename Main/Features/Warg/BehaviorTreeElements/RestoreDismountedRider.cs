using BehaviorTrees;
using BehaviorTrees.Nodes;
using BehaviorTreeWrapper.BlackBoardClasses;
using TAOM.Features.AdvancedCombat.BaseBehaviorTree;
using System;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Warg.BehaviorTreeElements;

/// <summary>
/// The "no rider" branch of a warg whose rider left it alive in the middle of a rage: the rage branch is never entered again
/// (it needs a rider), so nothing else would give the rider his formation back. Does nothing unless a rage is open.
/// </summary>
public class RestoreDismountedRider : BTTask, IBTBannerlordBase, IBTWargBlackboard, IBTInFormation, IBTMountBase
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

    public override BTTaskStatus Execute()
    {
        if (RageAttackStartTime.GetValue() == null) return BTTaskStatus.FinishedWithTrue;

        Agent rider = CleanIfEnemyDied.RiderToRestore(Agent.GetValue(), Rider.GetValue());
        // The player's rage swaps his controller; ending that is not this task's job.
        if (rider != null && rider.IsPlayerControlled) return BTTaskStatus.FinishedWithTrue;

        CleanIfEnemyDied.ResetAiControlledAgent(rider, AgentsFormation.GetValue());
        // Given back: a later warg death must not move him back to it from wherever he is by then.
        AgentsFormation.SetValue(null);
        RageAttackStartTime.SetValue(null);
        RageAttackAmount.SetValue(0);
        AgentHitBy.SetValue(null);
        return BTTaskStatus.FinishedWithTrue;
    }
}
