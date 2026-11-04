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
        if (AgentHitBy.GetValue() == null || AgentHitBy.GetValue().Health <= 0 || RageAttackAmount.GetValue() == 0)
        {
            RageAttackStartTime.SetValue(null);
            RageAttackAmount.SetValue(0);
            AgentHitBy.SetValue(null);

            if (Agent.GetValue().IsPlayerControlled)
                FinishRageMode.Reset(Agent.GetValue());
            else
            {
                ResetAiControlledAgent(RiderToRestore(Agent.GetValue(), Rider.GetValue()), AgentsFormation.GetValue());
                // Given back: a later warg death must not move him back to it from wherever he is by then.
                AgentsFormation.SetValue(null);
            }
        }
        return BTTaskStatus.FinishedWithTrue;
    }

    // The warg's live rider, else the one the rage start stored (he left a living warg mid-rage). The stored handle is held across
    // frames, so it must still be the slot's occupant, and in co-op only its owner restores it (as OnWargDied does).
    public static Agent RiderToRestore(Agent warg, Agent stored)
    {
        Agent live = warg?.RiderAgent;
        if (live != null) return live;
        if (stored == null || !AgentSlotIdentity.IsCurrentOccupant(stored) || !CoopMissionOwnership.IsOwnedHere(stored)) return null;
        return stored;
    }

    // A warg that dies without raging never stored a formation (the tree is built before its rider), so a null here means
    // "nothing to restore": the rider keeps the formation he still has.
    public static void ResetAiControlledAgent(Agent rider, Formation formation)
    {
        WargRageLog.Active?.Ended(rider, formation);
        if (rider == null || rider.State != TaleWorlds.Core.AgentState.Active) return;
        if (formation != null) rider.Formation = formation;
        rider.DisableScriptedMovement();
    }
}
