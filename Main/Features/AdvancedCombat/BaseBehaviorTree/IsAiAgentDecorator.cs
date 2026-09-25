using BehaviorTrees;
using BehaviorTreeWrapper.BlackBoardClasses;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.AdvancedCombat.BaseBehaviorTree;

/// <summary>
/// Passes when the tree's OWN agent is AI-controlled. For a creature on foot (the cave troll, #649);
/// <see cref="IsAiControlledDecorator"/> asks about a mount's rider and fails without one.
/// </summary>
internal class IsAiAgentDecorator : BTReturnFalseDecorator, IBTBannerlordBase
{
    BTBlackboardValue<Agent> _agent;
    public BTBlackboardValue<Agent> Agent { get => _agent; set => _agent = value; }

    public override bool Evaluate()
    {
        Agent agent = Agent.GetValue();
        return agent != null && agent.IsActive() && agent.IsAIControlled;
    }
}
