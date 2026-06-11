using System.Collections.Generic;
using BehaviorTrees;
using BehaviorTreeWrapper.BlackBoardClasses;
using TAOM.Adapters;
using TAOM.Features.AdvancedCombat;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Spider.BehaviorTreeElements;

/// <summary>
/// Engage gate for the ridden spider's bite: passes when a live non-mount enemy is within striking reach
/// (<see cref="SpiderConfig.BiteAttackRange"/> inside a <see cref="SpiderConfig.BiteConeAngleDegrees"/> cone)
/// and the spider is not already mid-bite. Warg <c>CheckOnceIfCanAttackEnemy</c> shape (SpatialGrid scan +
/// IsAttackLikelyToHit) with the elephant's anti-chain gate (Index compare via
/// <see cref="SpiderAttackActions.IsSpiderAttack"/> — never restart a clip that's still playing). The
/// spider's side is its RIDER's when ridden. Boundary code, like every warg/elephant BT element.
/// </summary>
public class SpiderCanBiteDecorator : BTReturnFalseDecorator, IBTBannerlordBase
{
    private IMissionAdapterFactory _adapterFactory;

    private BTBlackboardValue<Agent> _agent;
    public BTBlackboardValue<Agent> Agent { get => _agent; set => _agent = value; }

    public override bool Evaluate()
    {
        Agent spider = Agent.GetValue();
        if (spider == null || !spider.IsActive()) return false;

        // Anti-chain: while a bite clip is playing, never engage again (elephant review lesson —
        // Index compare against our own caches; zero-alloc, collision-immune).
        if (SpiderAttackActions.IsSpiderAttack(spider.GetCurrentAction(0))) return false;

        BattleSideEnum spiderSide = spider.RiderAgent?.Team.Side ?? spider.Team.Side;
        List<Agent> nearby = SpatialGrid.Instance.GetNearAliveAgentsInRange(
            (int)SpiderConfig.BiteTriggerScanRange, spider);

        _adapterFactory ??= IoC.Resolve<IMissionAdapterFactory>();
        foreach (Agent agent in nearby)
        {
            if (agent == spider || agent == spider.RiderAgent || agent.IsMount) continue;
            if (agent.IsActive() && agent.Team?.Side != spiderSide)
            {
                var targetAdapter = _adapterFactory.GetAgentAdapter(agent);
                var spiderAdapter = _adapterFactory.GetAgentAdapter(spider);
                if (targetAdapter.IsAttackLikelyToHit(spiderAdapter,
                        SpiderConfig.BiteConeAngleDegrees, SpiderConfig.BiteAttackRange))
                    return true;
            }
        }
        return false;
    }
}
