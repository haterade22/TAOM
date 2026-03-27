using BehaviorTrees;
using BehaviorTreeWrapper.BlackBoardClasses;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.AdvancedCombat.BaseBehaviorTree;

internal class IsPlayerControlledDecorator : BTReturnFalseDecorator, IBTBannerlordBase
{
    BTBlackboardValue<Agent> _agent;
    public BTBlackboardValue<Agent> Agent { get => _agent; set => _agent = value; }

    public override bool Evaluate()
    {
        Agent agent = Agent.GetValue();
        var mountedAgent = agent.RiderAgent;
        if (TaleWorlds.MountAndBlade.Agent.Main == null || mountedAgent == null) return false;
        return TaleWorlds.MountAndBlade.Agent.Main == mountedAgent;
    }
}
