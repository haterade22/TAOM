using BehaviorTrees;
using BehaviorTrees.Nodes;
using TAOM.Features.AdvancedCombat;
using System;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Warg.BehaviorTreeElements;

public class FinishRageMode : BTTask, IBTWargBlackboard
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
        AgentHitBy.SetValue(null);
        RageAttackAmount.SetValue(0);
        return BTTaskStatus.FinishedWithTrue;
    }

    public static void Reset(Agent warg)
    {
        if (Agent.Main != null && warg.Health > 0)
        {
            MBInformationManager.AddQuickInformation(new("You are able to control your warg again!"));
            InformationManager.DisplayMessage(new InformationMessage("You are able to control your warg again!"));
        }
        WargMissionBehavior.SwitchMainAgentController(false);
        AutonomousMovementPlayerController noMovementController = Mission.Current.GetMissionBehavior<AutonomousMovementPlayerController>();
        noMovementController.TargetAgent = null;
    }
}
