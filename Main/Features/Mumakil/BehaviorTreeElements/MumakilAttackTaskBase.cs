using System;
using BehaviorTrees;
using BehaviorTrees.Nodes;
using BehaviorTreeWrapper.BlackBoardClasses;
using TAOM.Features.AdvancedCombat;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Mumakil.BehaviorTreeElements;

/// <summary>
/// Shared template for Mûmakil attacks: plays the derived class's attack animation on channel 0, stamps the
/// derived class's cooldown, and deals radial knockdown damage (`CustomAttacksUtils.TakeDamage`) to every live
/// enemy within <see cref="MumakilConfig.TrampleRadius"/>. Damage amount from the pure
/// <see cref="IMumakilAttackService.ComputeInflictedDamage"/> (shield-block-aware). Boundary code, 1-for-1 with
/// the elephant's <c>ElephantAttackTaskBase</c>.
/// </summary>
public abstract class MumakilAttackTaskBase : BTTask, IBTBannerlordBase, IBTMumakilBlackboard
{
    private readonly MBList<Agent> _scratch = new();
    private IMumakilAttackService _service;

    private BTBlackboardValue<Agent> _agent;
    private BTBlackboardValue<DateTime?> _trampleLastFired;
    private BTBlackboardValue<DateTime?> _sideAttackLastFired;
    private BTBlackboardValue<float> _targetBearing;
    public BTBlackboardValue<Agent> Agent { get => _agent; set => _agent = value; }
    public BTBlackboardValue<DateTime?> TrampleLastFired { get => _trampleLastFired; set => _trampleLastFired = value; }
    public BTBlackboardValue<DateTime?> SideAttackLastFired { get => _sideAttackLastFired; set => _sideAttackLastFired = value; }
    public BTBlackboardValue<float> TargetBearing { get => _targetBearing; set => _targetBearing = value; }

    /// <summary>The attack animation to play this firing (side attacks pick by <see cref="TargetBearing"/>).</summary>
    protected abstract ActionIndexCache GetAttackAction();

    /// <summary>Stamp this attack kind's cooldown (write DateTime.Now into the matching blackboard value).</summary>
    protected abstract void StampCooldown(DateTime now);

    /// <summary>Which attack this task is — selects the damage band in <see cref="IMumakilAttackService.ComputeInflictedDamage"/>.</summary>
    protected abstract MumakilAttackKind AttackKind { get; }

    public override BTTaskStatus Execute()
    {
        Agent mumakil = Agent.GetValue();
        if (mumakil == null || !mumakil.IsActive()) return BTTaskStatus.FinishedWithFalse;
        Agent rider = mumakil.RiderAgent;
        if (rider == null) return BTTaskStatus.FinishedWithFalse;

        mumakil.SetActionChannel(0, GetAttackAction());
        StampCooldown(DateTime.Now);

        _service ??= IoC.Resolve<IMumakilAttackService>();
        Mission.Current.GetNearbyAgents(mumakil.Position.AsVec2, MumakilConfig.TrampleRadius, _scratch);
        foreach (Agent victim in _scratch)
        {
            if (victim == null || victim == mumakil || !victim.IsActive() || !victim.IsEnemyOf(rider)) continue;
            // Only a SHIELD block reduces the damage; weapon parries take full damage.
            // (Fully-qualified — the `Agent` blackboard property shadows the Agent type.)
            bool blocking = victim.GetCurrentActionType(1) == TaleWorlds.MountAndBlade.Agent.ActionCodeType.DefendShield;
            // Roll per victim so each enemy caught in the radius takes an independent hit within the kind's band.
            int damage = _service.ComputeInflictedDamage(AttackKind, blocking, MBRandom.RandomFloat);
            CustomAttacksUtils.TakeDamage(victim, mumakil, damage, MumakilConfig.TrampleBlowMagnitude, knockDown: !blocking);
        }
        return BTTaskStatus.FinishedWithTrue;
    }
}

/// <summary>
/// The trample (double-sweep thrash) — the priority attack, 10s cooldown. Alternates randomly between the two
/// near-identical thrash clips (attack_3/attack_4) for variety.
/// </summary>
public class MumakilTrampleTask : MumakilAttackTaskBase
{
    protected override ActionIndexCache GetAttackAction()
        => MBRandom.RandomFloat < 0.5f ? MumakilAttackActions.Trample : MumakilAttackActions.TrampleAlt;

    protected override void StampCooldown(DateTime now) => TrampleLastFired.SetValue(now);

    protected override MumakilAttackKind AttackKind => MumakilAttackKind.Trample;
}

/// <summary>
/// Left/right tusk swing — fired while the trample recharges, 4s cooldown. Picks the swing matching the best
/// enemy's bearing (written by <see cref="MumakilEngageDecorator"/>): positive = LEFT, negative = RIGHT.
/// </summary>
public class MumakilSideAttackTask : MumakilAttackTaskBase
{
    protected override ActionIndexCache GetAttackAction()
        => TargetBearing.GetValue() >= 0f ? MumakilAttackActions.SwingLeft : MumakilAttackActions.SwingRight;

    protected override void StampCooldown(DateTime now) => SideAttackLastFired.SetValue(now);

    protected override MumakilAttackKind AttackKind => MumakilAttackKind.SideAttack;
}
