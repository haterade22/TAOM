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

public class PrepareOnFirstCall : BTTask, IBTBannerlordBase, IBTWargBlackboard, IBTInFormation, IBTMountBase
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
        if (FirstAttack.GetValue())
        {
            FirstAttack.SetValue(false);

            if (Agent.GetValue().IsPlayerControlled)
            {
                MBInformationManager.AddQuickInformation(new("Your warg entered into a rage, you lost control!"));
                InformationManager.DisplayMessage(new InformationMessage("Your warg entered into a rage, you lost control!"));
                WargMissionBehavior.SwitchMainAgentController(true);
                var controller = Mission.Current.GetMissionBehavior<IAutonomousMovementController>();
                if (controller != null) controller.TargetAgent = AgentHitBy.GetValue();
                // The no-rider branch and OnWargDied find whose controller this rage holds through this.
                Rider.SetValue(Agent.GetValue().RiderAgent);
            }
            else
            {
                Agent rider = Agent.GetValue()?.RiderAgent;
                if (rider != null)
                {
                    // The tree was built before the rider's formation was set, so its stored formation is null for a warg spawned after
                    // the first scan. A hit in the second after a rage's last attack starts the next rage before the first closes: this
                    // rider has no formation by then, and the stored one is still his.
                    if (rider.Formation != null || !ReferenceEquals(Rider.GetValue(), rider))
                        AgentsFormation.SetValue(rider.Formation);
                    // A rider who leaves the living warg mid-rage is no longer its RiderAgent: the reset finds him through this.
                    Rider.SetValue(rider);
                    WargRageLog.Active?.Started(rider, AgentsFormation.GetValue() != null);
                    rider.Formation = null;
                }
            }
        }
        return BTTaskStatus.FinishedWithTrue;
    }
}
