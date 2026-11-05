using BehaviorTrees;
using BehaviorTreeWrapper.BlackBoardClasses;
using TAOM.Features.AdvancedCombat;
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Warg.BehaviorTreeElements;

public class NoEnemyCloseDecorator : BTReturnFalseDecorator, IBTBannerlordBase
{
    BTBlackboardValue<Agent> _agent;
    // Reused 60 m scan buffer: this runs on every tick of every ridden warg's tree. SpatialGrid
    // clears and refills it on every call, so no handle from an earlier tick is ever read.
    private readonly List<Agent> _scratch = new();
    public BTBlackboardValue<Agent> Agent { get => _agent; set => _agent = value; }

    public override bool Evaluate()
    {
        Agent agent = Agent.GetValue();
        SpatialGrid.Instance.GetNearAliveAgentsInRange(60, agent, _scratch);
        Team wargTeam = agent.RiderAgent?.Team ?? agent.Team;
        foreach (Agent agent2 in _scratch)
        {
            if (agent2 == agent || agent2 == agent.RiderAgent)
                continue;
            if (agent2.IsActive() && agent2.Team != null && agent2.Team != wargTeam)
                return false;
        }
        return true;
    }
}
