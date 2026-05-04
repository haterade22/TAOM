using BehaviorTrees;
using BehaviorTreeWrapper.BlackBoardClasses;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.AdvancedCombat.BaseBehaviorTree;
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Spider.BehaviorTreeElements;

/// <summary>
/// Returns false (so its child runs) when an enemy agent is within
/// <see cref="SpiderConfig.TargetDetectionRange"/> of the spider.
/// Mirrors NoEnemyCloseDecorator (Warg) but uses a smaller spider-specific range
/// and treats the spider's own team (no rider) as the friendly team.
/// </summary>
public class NoEnemyNearSpiderDecorator : BTReturnFalseDecorator, IBTBannerlordBase, IBTSpiderBlackboard
{
    BTBlackboardValue<Agent> _agent;
    public BTBlackboardValue<Agent> Agent { get => _agent; set => _agent = value; }

    public override bool Evaluate()
    {
        Agent agent = Agent.GetValue();
        if (agent == null || !agent.IsActive()) return true;
        if (SpatialGrid.Instance == null) return true;

        // Use a wider scan range than the attack range so spiders detect enemies
        // before they're already in bite range.
        List<Agent> nearby = SpatialGrid.Instance.GetNearAliveAgentsInRange(SpiderConfig.TargetDetectionRange * 4f, agent);
        Team spiderTeam = agent.Team;
        foreach (Agent candidate in nearby)
        {
            if (candidate == agent) continue;
            if (!candidate.IsActive()) continue;
            if (candidate.Team == null || candidate.Team == spiderTeam) continue;
            return false; // enemy in range — child should execute
        }
        return true; // no enemy — return-false-decorator returns false from parent's POV (skip child)
    }
}
