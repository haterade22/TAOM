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
    // Resolved once when the tree is built (once per warg per mission), never per evaluation: the
    // tree's root runs every mission tick. An instance field, not a static, so a module reload can
    // never keep a factory from a disposed container.
    private readonly IMissionAdapterFactory _adapterFactory = IoC.Resolve<IMissionAdapterFactory>();
    // Reused scan buffer. SpatialGrid clears and refills it on every call, so no handle from an
    // earlier tick is ever read.
    private readonly List<Agent> _scratch = new();

    public PeriodicallyCheckIfCanAttackAnyone() : base(0.2) { }
    public BTBlackboardValue<Agent> Agent { get => _agent; set => _agent = value; }
    public override bool Evaluate()
    {
        Agent warg = Agent.GetValue();
        BattleSideEnum wargSide = warg.RiderAgent?.Team.Side ?? warg.Team.Side;
        SpatialGrid.Instance.GetNearAliveAgentsInRange(10, warg, _scratch);
        // Looked up once, at the first candidate: a scan with no candidate pays no cache lookup.
        IAgentAdapter wargAdapter = null;
        foreach (Agent agent in _scratch)
        {
            if (agent == warg || agent == warg.RiderAgent || agent.IsMount) continue;
            if (agent.IsActive() && agent.Team?.Side != wargSide)
            {
                wargAdapter ??= _adapterFactory.GetAgentAdapter(warg);
                var agentAdapter = _adapterFactory.GetAgentAdapter(agent);
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
    // Resolved once when the tree is built (once per warg per mission), never per evaluation: the
    // tree's root runs every mission tick. An instance field, not a static, so a module reload can
    // never keep a factory from a disposed container.
    private readonly IMissionAdapterFactory _adapterFactory = IoC.Resolve<IMissionAdapterFactory>();
    // Reused scan buffer. SpatialGrid clears and refills it on every call, so no handle from an
    // earlier tick is ever read.
    private readonly List<Agent> _scratch = new();

    public BTBlackboardValue<Agent> Agent { get => _agent; set => _agent = value; }

    public override bool Evaluate()
    {
        Agent warg = Agent.GetValue();
        SpatialGrid.Instance.GetNearAliveAgentsInRange(10, warg, _scratch);
        BattleSideEnum wargSide = warg.RiderAgent?.Team.Side ?? warg.Team.Side;
        // Looked up once, at the first candidate: a scan with no candidate pays no cache lookup.
        IAgentAdapter wargAdapter = null;
        foreach (Agent agent in _scratch)
        {
            if (agent == warg || agent == warg.RiderAgent) continue;
            if (agent.IsActive() && agent.Team?.Side != wargSide && !agent.IsMount)
            {
                wargAdapter ??= _adapterFactory.GetAgentAdapter(warg);
                var agentAdapter = _adapterFactory.GetAgentAdapter(agent);
                bool likelyToHit = agentAdapter.IsAttackLikelyToHit(wargAdapter, 30, WargConfig.WargAttackRange);
                if (likelyToHit)
                    return true;
            }
        }
        return false;
    }
}
