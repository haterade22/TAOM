using BehaviorTrees;
using BehaviorTrees.Nodes;
using BehaviorTreeWrapper.BlackBoardClasses;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.AdvancedCombat.BaseBehaviorTree;
using TAOM.Features.CoopInterop.Missions;
using System;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Warg.BehaviorTreeElements;

public class CleanIfEnemyDied : BTTask, IBTBannerlordBase, IBTWargBlackboard, IBTInFormation, IBTMountBase
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
        if (ShouldClose())
        {
            RageAttackStartTime.SetValue(null);
            RageAttackAmount.SetValue(0);
            AgentHitBy.SetValue(null);
            FirstAttack.SetValue(false);

            Agent warg = Agent.GetValue();
            if (warg.IsPlayerControlled)
                FinishRageMode.Reset(warg);
            else
                GiveFormationBack(warg, Rider.GetValue(), AgentsFormation.GetValue());
            // Given back: a later warg death must not move him back to it from wherever he is by then.
            AgentsFormation.SetValue(null);
            Rider.SetValue(null);
        }
        return BTTaskStatus.FinishedWithTrue;
    }

    protected virtual bool ShouldClose() =>
        AgentHitBy.GetValue() == null || AgentHitBy.GetValue().Health <= 0 || RageAttackAmount.GetValue() == 0;

    // The formation is the stored rider's, so he gets it back even when he has left the warg. A rider who took his place lost
    // nothing (CloseRageOnNewRider runs before the rage clears anything on him), so he keeps his own.
    public static void GiveFormationBack(Agent warg, Agent stored, Formation formation)
    {
        Agent owner = ValidStoredRider(stored);
        if (owner != null) ResetAiControlledAgent(owner, formation);
        Agent live = warg?.RiderAgent;
        if (live != null && live != owner && CoopMissionOwnership.IsOwnedHere(live)) ResetAiControlledAgent(live, null);
    }

    // The stored handle is held across frames, so it must still be the slot's occupant, and in co-op only its owner restores it
    // (as OnWargDied does). Null otherwise.
    public static Agent ValidStoredRider(Agent stored)
    {
        if (stored == null || !AgentSlotIdentity.IsCurrentOccupant(stored) || !CoopMissionOwnership.IsOwnedHere(stored)) return null;
        return stored;
    }

    // A null formation means "nothing to restore": the rider keeps the one he has. A panicked rider stays out of it, because
    // Agent.OnFleeing cleared his on purpose.
    public static void ResetAiControlledAgent(Agent rider, Formation formation)
    {
        WargRageLog.Active?.Ended(rider, formation);
        if (rider == null || rider.State != TaleWorlds.Core.AgentState.Active) return;
        if (formation != null && !rider.IsRunningAway) rider.Formation = formation;
        rider.DisableScriptedMovement();
    }
}
