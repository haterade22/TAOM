using System;
using System.Collections.Generic;
using BehaviorTrees;
using BehaviorTreeWrapper.BlackBoardClasses;
using TAOM.Adapters;
using TAOM.Features.AdvancedCombat;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Spider.BehaviorTreeElements;

/// <summary>
/// Engage gate for the spider's directional attacks: passes when a live non-mount enemy is within striking reach
/// (<see cref="SpiderConfig.BiteAttackRange"/> inside a <see cref="SpiderConfig.BiteConeAngleDegrees"/> cone) and
/// the spider is not already mid-attack. On a passing scan it writes the NEAREST engageable enemy's signed bearing
/// to the blackboard so <see cref="SpiderSideAttackTask"/> can pick the matching left/right swipe. Keeps the
/// spider's warg-shaped cone engage (SpatialGrid scan + IsAttackLikelyToHit) and adds the elephant's bearing
/// computation + anti-chain gate (Index compare via <see cref="SpiderAttackActions.IsSpiderAttack"/> — never
/// restart a clip that's still playing). The spider's side is its RIDER's when ridden. Boundary code, like every
/// warg/elephant BT element.
/// </summary>
public class SpiderEngageDecorator : BTReturnFalseDecorator, IBTBannerlordBase, IBTSpiderBlackboard
{
    private IMissionAdapterFactory _adapterFactory;
    // Reusable scan buffer — the engage gate evaluates ~5×/sec per spider (the idle SleepTask cadence). Filling a
    // field buffer avoids a fresh List<Agent> allocation every scan (elephant-parity; the warg/old-spider pattern
    // allocated per-eval). Adapter wrappers are already cached by MissionAdapterFactory.GetAgentAdapter.
    private readonly List<Agent> _scratch = new();

    private BTBlackboardValue<Agent> _agent;
    private BTBlackboardValue<DateTime?> _pounceLastFired;
    private BTBlackboardValue<DateTime?> _sideAttackLastFired;
    private BTBlackboardValue<float> _targetBearing;
    public BTBlackboardValue<Agent> Agent { get => _agent; set => _agent = value; }
    public BTBlackboardValue<DateTime?> PounceLastFired { get => _pounceLastFired; set => _pounceLastFired = value; }
    public BTBlackboardValue<DateTime?> SideAttackLastFired { get => _sideAttackLastFired; set => _sideAttackLastFired = value; }
    public BTBlackboardValue<float> TargetBearing { get => _targetBearing; set => _targetBearing = value; }

    public override bool Evaluate()
    {
        Agent spider = Agent.GetValue();
        if (spider == null || !spider.IsActive()) return false;

        // Anti-chain: while an attack clip is playing, never engage again (elephant lesson — Index compare against
        // our own caches; zero-alloc, collision-immune).
        if (SpiderAttackActions.IsSpiderAttack(spider.GetCurrentAction(0))) return false;

        BattleSideEnum spiderSide = spider.RiderAgent?.Team?.Side ?? spider.Team?.Side ?? BattleSideEnum.None;
        SpatialGrid.Instance.GetNearAliveAgentsInRange((int)SpiderConfig.BiteTriggerScanRange, spider, _scratch);

        _adapterFactory ??= IoC.Resolve<IMissionAdapterFactory>();
        var spiderAdapter = _adapterFactory.GetAgentAdapter(spider);

        Agent best = null;
        float bestDist = float.MaxValue;
        foreach (Agent agent in _scratch)
        {
            if (agent == spider || agent == spider.RiderAgent || agent.IsMount) continue;
            if (!agent.IsActive() || agent.Team?.Side == spiderSide) continue;

            var targetAdapter = _adapterFactory.GetAgentAdapter(agent);
            if (!targetAdapter.IsAttackLikelyToHit(spiderAdapter, SpiderConfig.BiteConeAngleDegrees, SpiderConfig.BiteAttackRange))
                continue;

            float dist = (agent.Position - spider.Position).Length;
            if (dist < bestDist)
            {
                bestDist = dist;
                best = agent;
            }
        }

        if (best == null) return false;

        // Signed bearing: z of cross(lookDir, toEnemy). POSITIVE = enemy on the spider's LEFT (counter-clockwise,
        // Z-up right-handed), NEGATIVE = RIGHT. Consumed by SpiderSideAttackTask to pick the matching swipe.
        Vec3 lookDir = spider.LookDirection;
        Vec3 toEnemy = (best.Position - spider.Position).NormalizedCopy();
        TargetBearing.SetValue(lookDir.x * toEnemy.y - lookDir.y * toEnemy.x);
        return true;
    }
}
