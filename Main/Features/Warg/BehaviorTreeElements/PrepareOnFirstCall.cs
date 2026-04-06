using BehaviorTrees;
using BehaviorTrees.Nodes;
using BehaviorTreeWrapper.BlackBoardClasses;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.AdvancedCombat.BaseBehaviorTree;
using System;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Warg.BehaviorTreeElements;

public class PrepareOnFirstCall : BTTask, IBTBannerlordBase, IBTWargBlackboard, IBTInFormation
{
    BTBlackboardValue<Agent> _agent;
    BTBlackboardValue<Agent> _agentHitBy;
    BTBlackboardValue<int> _rageAttackAmount;
    BTBlackboardValue<DateTime?> _rageAttackStartTime;
    BTBlackboardValue<bool> _firstAttack;
    BTBlackboardValue<Formation> _agentsFormation;
    public BTBlackboardValue<Agent> Agent { get => _agent; set => _agent = value; }
    public BTBlackboardValue<Agent> AgentHitBy { get => _agentHitBy; set => _agentHitBy = value; }
    public BTBlackboardValue<int> RageAttackAmount { get => _rageAttackAmount; set => _rageAttackAmount = value; }
    public BTBlackboardValue<DateTime?> RageAttackStartTime { get => _rageAttackStartTime; set => _rageAttackStartTime = value; }
    public BTBlackboardValue<bool> FirstAttack { get => _firstAttack; set => _firstAttack = value; }
    public BTBlackboardValue<Formation> AgentsFormation { get => _agentsFormation; set => _agentsFormation = value; }

    public override BTTaskStatus Execute()
    {
        if (FirstAttack.GetValue())
        {
            FirstAttack.SetValue(false);

            if (Agent.GetValue().IsPlayerControlled)
            {
                MBInformationManager.AddQuickInformation(new("Your warg entered into a rage, you lost control!"));
                InformationManager.DisplayMessage(new InformationMessage("Your warg entered into a rage, you lost control!"));
                WargMissionBehavior.SwitchMainAgentController(true);
                AutonomousMovementPlayerController noMovementController = Mission.Current.GetMissionBehavior<AutonomousMovementPlayerController>();
                noMovementController.TargetAgent = AgentHitBy.GetValue();
            }
            else
            {
                Agent rider = Agent.GetValue()?.RiderAgent;
                if (rider != null) rider.Formation = null;
            }
        }
        return BTTaskStatus.FinishedWithTrue;
    }
}
