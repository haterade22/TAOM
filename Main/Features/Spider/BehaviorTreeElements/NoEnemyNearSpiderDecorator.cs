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
        Mission mission = Mission.Current;
        if (mission == null) return true;

        // Engage whenever ANY enemy exists in the mission, not just within a 16m scan: a detached spider
        // must advance toward the enemy line from across the battlefield (SpiderMoveToEnemyTask does the
        // distance-agnostic advance). The old 16m scan meant the engage branch never ran at battle start
        // (enemies far away) so the spiders idled and mount-wandered.
        Team spiderTeam = agent.Team;
        foreach (Agent candidate in mission.Agents)
        {
            if (candidate == agent || !candidate.IsActive()) continue;
            if (candidate.Team == null || candidate.Team == spiderTeam) continue;
            return false; // an enemy exists — run the engage branch (move + bite)
        }
        return true; // no enemies left — idle
    }
}
