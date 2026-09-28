using BehaviorTrees;
using BehaviorTreeWrapper.BlackBoardClasses;
using TAOM.Features.CreatureBandits.Hooks;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CreatureBandits.BehaviorTreeElements;

/// <summary>
/// Passes while the agent is a live, AI-controlled creature bandit (#692). This tree is attached only to creature
/// bandits (<see cref="CreatureBanditMissionBehavior"/>); one that dies, loses AI control, or takes a rider (possible
/// only when route A was skipped) falls to the tree's idle branch.
/// </summary>
public class IsCreatureBanditDecorator : BTReturnFalseDecorator, IBTBannerlordBase
{
    private BTBlackboardValue<Agent> _agent;
    public BTBlackboardValue<Agent> Agent { get => _agent; set => _agent = value; }

    public override bool Evaluate()
    {
        Agent agent = Agent.GetValue();
        return agent != null && agent.IsActive() && agent.IsAIControlled && CreatureBanditAgents.Is(agent);
    }
}
