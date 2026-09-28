using System;
using BehaviorTrees;
using BehaviorTrees.Nodes;
using BehaviorTreeWrapper.BlackBoardClasses;
using TAOM.Adapters;
using TAOM.Features.CreatureBandits.Diagnostics;
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
    // The strike rules, read at each attack so a tuning change applies to the next one; null is the ridden spider's.
    private readonly Func<SpiderStrikeSet>? _strikes;

    protected SpiderAttackTaskBase(Func<SpiderStrikeSet>? strikes) => _strikes = strikes;

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
        float bearing = TargetBearing.GetValue();
        float velocityY = spider.MovementVelocity.Y;
        var outcome = _service.SpiderAttack(spiderAdapter, Kind, bearing, _strikes?.Invoke() ?? SpiderStrikeSet.Ridden);
        // Creature Bandits diagnostics (#692, temporary): a creature's budgeted attack line, here at the boundary that
        // holds the agent. The ridden spider has no strike set and no diagnostics record.
        if (_strikes != null && outcome.Clip != null)
            CreatureBanditDiag.NoteAttack(spider, Kind.ToString(), outcome.Clip, outcome.InArc, outcome.Allies, outcome.Struck,
                outcome.MaxTargets, velocityY, bearing, Mission.Current?.CurrentTime ?? float.NaN);
        return BTTaskStatus.FinishedWithTrue;
    }
}

/// <summary>The priority lunge — front bite, or the charge variant at speed. Long cooldown.</summary>
public class SpiderPounceTask : SpiderAttackTaskBase
{
    public SpiderPounceTask(Func<SpiderStrikeSet>? strikes = null) : base(strikes) { }
    protected override SpiderAttackKind Kind => SpiderAttackKind.Pounce;
    protected override void StampCooldown(DateTime now) => PounceLastFired.SetValue(now);
}

/// <summary>Left/right swipe picked by the best enemy's bearing (written by <see cref="SpiderEngageDecorator"/>),
/// positive = LEFT, negative = RIGHT. Short cooldown — fills the gap while the pounce recharges.</summary>
public class SpiderSideAttackTask : SpiderAttackTaskBase
{
    public SpiderSideAttackTask(Func<SpiderStrikeSet>? strikes = null) : base(strikes) { }
    protected override SpiderAttackKind Kind => SpiderAttackKind.SideAttack;
    protected override void StampCooldown(DateTime now) => SideAttackLastFired.SetValue(now);
}
