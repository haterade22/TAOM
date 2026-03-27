using BehaviorTrees;
using BehaviorTreeWrapper.BlackBoardClasses;
using BehaviorTreeWrapper.Decorators;
using TAOM.Adapters;
using TAOM.Features.AdvancedCombat;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Warg.BehaviorTreeElements;

public class PeriodicallyCheckIfCanAttackAnyone : WaitNSecondsTickDecorator, IBTBannerlordBase
{
    BTBlackboardValue<Agent> _agent;
    private static IMissionAdapterFactory AdapterFactory => IoC.Resolve<IMissionAdapterFactory>();

    public PeriodicallyCheckIfCanAttackAnyone() : base(0.2) { }
    public BTBlackboardValue<Agent> Agent { get => _agent; set => _agent = value; }
    public override bool Evaluate()
    {
        Agent warg = Agent.GetValue();
        List<Agent> nearbyAgents = SpatialGrid.Instance.GetNearAliveAgentsInRange(10, warg);
        foreach (Agent agent in nearbyAgents)
        {
            if (agent == warg || agent == warg.RiderAgent || agent.IsMount) continue;
            if (agent.IsActive())
            {
                var agentAdapter = AdapterFactory.GetAgentAdapter(agent);
                var wargAdapter = AdapterFactory.GetAgentAdapter(warg);
                bool likelyToHit = agentAdapter.IsAttackLikelyToHit(wargAdapter, 30, WargConfig.WargAttackRange);
                if (likelyToHit)
                    return true;
            }
        }
        return false;
    }

    public override void Notify(object[] data) { }
}

public class CheckOnceIfCanAttackEnemy : BTReturnFalseDecorator, IBTBannerlordBase
{
    BTBlackboardValue<Agent> _agent;
    private static IMissionAdapterFactory AdapterFactory => IoC.Resolve<IMissionAdapterFactory>();

    public BTBlackboardValue<Agent> Agent { get => _agent; set => _agent = value; }

    public override bool Evaluate()
    {
        Agent warg = Agent.GetValue();
        List<Agent> nearbyAgents = SpatialGrid.Instance.GetNearAliveAgentsInRange(10, warg);
        BattleSideEnum wargSide = warg.RiderAgent?.Team.Side ?? warg.Team.Side;
        foreach (Agent agent in nearbyAgents)
        {
            if (agent == warg || agent == warg.RiderAgent) continue;
            if (agent.IsActive() && agent.Team?.Side != wargSide && !agent.IsMount)
            {
                var agentAdapter = AdapterFactory.GetAgentAdapter(agent);
                var wargAdapter = AdapterFactory.GetAgentAdapter(warg);
                bool likelyToHit = agentAdapter.IsAttackLikelyToHit(wargAdapter, 30, WargConfig.WargAttackRange);
                if (likelyToHit)
                    return true;
            }
        }
        return false;
    }
}
