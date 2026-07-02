using System;
using BehaviorTrees;
using BehaviorTrees.Nodes;
using BehaviorTreeWrapper.BlackBoardClasses;
using TAOM.Adapters;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Spider.BehaviorTreeElements;

/// <summary>
/// Shared template for spider attacks: stamps the derived class's cooldown, then fires the bone-collision
/// CustomAttack for the derived kind via <see cref="ISpiderAttackService.SpiderAttack"/> (the service resolves the
/// clip + bones by kind/velocity/bearing and applies damage through HandleSpiderTargetHit → the NaN-guarded
/// CustomAttacksUtils.TakeDamage). Boundary code, mirroring the shared ElephantLikeAttackTaskBase — but keeps the
/// spider's bone-collision damage instead of the elephant's radial AoE.
/// </summary>
public abstract class SpiderAttackTaskBase : BTTask, IBTBannerlordBase, IBTSpiderBlackboard
{
    private IMissionAdapterFactory _adapterFactory;
    private ISpiderAttackService _service;

    private BTBlackboardValue<Agent> _agent;
    private BTBlackboardValue<DateTime?> _pounceLastFired;
    private BTBlackboardValue<DateTime?> _sideAttackLastFired;
    private BTBlackboardValue<float> _targetBearing;
    public BTBlackboardValue<Agent> Agent { get => _agent; set => _agent = value; }
    public BTBlackboardValue<DateTime?> PounceLastFired { get => _pounceLastFired; set => _pounceLastFired = value; }
    public BTBlackboardValue<DateTime?> SideAttackLastFired { get => _sideAttackLastFired; set => _sideAttackLastFired = value; }
    public BTBlackboardValue<float> TargetBearing { get => _targetBearing; set => _targetBearing = value; }

    /// <summary>The attack kind this task fires (drives the service's clip/bone selection + which cooldown is stamped).</summary>
    protected abstract SpiderAttackKind Kind { get; }

    /// <summary>Stamp this kind's cooldown (write DateTime.Now into the matching blackboard value).</summary>
    protected abstract void StampCooldown(DateTime now);

    public override BTTaskStatus Execute()
    {
        Agent spider = Agent.GetValue();
        if (spider == null || !spider.IsActive()) return BTTaskStatus.FinishedWithFalse;

        StampCooldown(DateTime.Now);

        _adapterFactory ??= IoC.Resolve<IMissionAdapterFactory>();
        _service ??= IoC.Resolve<ISpiderAttackService>();
        var spiderAdapter = _adapterFactory.GetAgentAdapter(spider);
        // bearing only steers a SideAttack; a Pounce ignores it (clip chosen by speed).
        _service.SpiderAttack(spiderAdapter, Kind, TargetBearing.GetValue());
        return BTTaskStatus.FinishedWithTrue;
    }
}

/// <summary>The priority lunge — front bite, or the charge variant at speed. Long cooldown.</summary>
public class SpiderPounceTask : SpiderAttackTaskBase
{
    protected override SpiderAttackKind Kind => SpiderAttackKind.Pounce;
    protected override void StampCooldown(DateTime now) => PounceLastFired.SetValue(now);
}

/// <summary>Left/right swipe picked by the best enemy's bearing (written by <see cref="SpiderEngageDecorator"/>),
/// positive = LEFT, negative = RIGHT. Short cooldown — fills the gap while the pounce recharges.</summary>
public class SpiderSideAttackTask : SpiderAttackTaskBase
{
    protected override SpiderAttackKind Kind => SpiderAttackKind.SideAttack;
    protected override void StampCooldown(DateTime now) => SideAttackLastFired.SetValue(now);
}
