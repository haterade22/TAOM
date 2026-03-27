using BehaviorTrees;
using BehaviorTreeWrapper.BlackBoardClasses;
using TAOM.Features.AdvancedCombat;
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Warg.BehaviorTreeElements;

public class NoEnemyCloseDecorator : BTReturnFalseDecorator, IBTBannerlordBase
{
    BTBlackboardValue<Agent> _agent;
    public BTBlackboardValue<Agent> Agent { get => _agent; set => _agent = value; }

    public override bool Evaluate()
    {
        Agent agent = Agent.GetValue();
        List<Agent> nearbyAgents = SpatialGrid.Instance.GetNearAliveAgentsInRange(60, agent);
        Team wargTeam = agent.RiderAgent?.Team ?? agent.Team;
        foreach (Agent agent2 in nearbyAgents)
        {
            if (agent2 == agent || agent2 == agent.RiderAgent)
                continue;
            if (agent2.IsActive() && agent2.Team != null && agent2.Team != wargTeam)
                return false;
        }
        return true;
    }
}
