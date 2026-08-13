using System;
using BehaviorTrees;
using BehaviorTrees.Nodes;
using BehaviorTreeWrapper.BlackBoardClasses;
using TAOM.Features.AdvancedCombat;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.ElephantLike.BehaviorTreeElements;

/// <summary>
/// Shared template for elephant-like attacks: plays the derived class's attack animation on channel 0, stamps the
/// derived class's cooldown, and deals radial knockdown damage (`CustomAttacksUtils.TakeDamage`) to every live
/// enemy within the profile's <see cref="ElephantLikeCombatProfile.TrampleRadius"/>. Damage amount from the pure
/// <see cref="IElephantLikeAttackService.ComputeInflictedDamage"/> (ADOD_Beasts's formula,
/// shield-block-aware). Boundary code, mirroring the warg's <c>WargAttackTask</c>.
/// </summary>
public abstract class ElephantLikeAttackTaskBase : BTTask, IBTBannerlordBase, IBTElephantLikeBlackboard
{
    private readonly MBList<Agent> _scratch = new();
    private IElephantLikeAttackService _service;

    /// <summary>The creature's boundary tuning (ranges, clips, service resolver).</summary>
    protected readonly ElephantLikeCombatProfile Profile;

    private BTBlackboardValue<Agent> _agent;
    private BTBlackboardValue<DateTime?> _trampleLastFired;
    private BTBlackboardValue<DateTime?> _sideAttackLastFired;
    private BTBlackboardValue<float> _targetBearing;
    public BTBlackboardValue<Agent> Agent { get => _agent; set => _agent = value; }
    public BTBlackboardValue<DateTime?> TrampleLastFired { get => _trampleLastFired; set => _trampleLastFired = value; }
    public BTBlackboardValue<DateTime?> SideAttackLastFired { get => _sideAttackLastFired; set => _sideAttackLastFired = value; }
    public BTBlackboardValue<float> TargetBearing { get => _targetBearing; set => _targetBearing = value; }

    protected ElephantLikeAttackTaskBase(ElephantLikeCombatProfile profile) => Profile = profile;

    /// <summary>The attack animation to play this firing (side attacks pick by <see cref="TargetBearing"/>).</summary>
    protected abstract ActionIndexCache GetAttackAction();

    /// <summary>Stamp this attack kind's cooldown (write DateTime.Now into the matching blackboard value).</summary>
    protected abstract void StampCooldown(DateTime now);

    /// <summary>Which attack this task is — selects the damage band in <see cref="IElephantLikeAttackService.ComputeInflictedDamage"/>.</summary>
    protected abstract ElephantLikeAttackKind AttackKind { get; }

    public override BTTaskStatus Execute()
    {
        Agent creature = Agent.GetValue();
        if (creature == null || !creature.IsActive()) return BTTaskStatus.FinishedWithFalse;
        Agent rider = creature.RiderAgent;
        if (rider == null) return BTTaskStatus.FinishedWithFalse;

        creature.SetActionChannel(0, GetAttackAction());
        StampCooldown(DateTime.Now);

        _service ??= Profile.ResolveService();
        Mission.Current.GetNearbyAgents(creature.Position.AsVec2, Profile.TrampleRadius, _scratch);
        foreach (Agent victim in _scratch)
        {
            if (victim == null || victim == creature || !victim.IsActive() || !victim.IsEnemyOf(rider)) continue;
            // Upstream-pack parity: only a SHIELD block reduces the damage; weapon parries take full damage.
            // (Fully-qualified — the `Agent` blackboard property shadows the Agent type.)
            bool blocking = victim.GetCurrentActionType(1) == TaleWorlds.MountAndBlade.Agent.ActionCodeType.DefendShield;
            // Roll per victim so each enemy caught in the radius takes an independent hit within the kind's band.
            int damage = _service.ComputeInflictedDamage(AttackKind, blocking, MBRandom.RandomFloat);
            CustomAttacksUtils.TakeDamage(victim, creature, damage, Profile.TrampleBlowMagnitude, knockDown: !blocking);
        }
        return BTTaskStatus.FinishedWithTrue;
    }
}

/// <summary>
/// The trample (double-sweep thrash) — the priority attack, 10s cooldown. Alternates randomly between the two
/// near-identical thrash clips (attack_3/attack_4) for variety; ADOD_Beasts never played attack_4 at all.
/// </summary>
public class ElephantLikeTrampleTask : ElephantLikeAttackTaskBase
{
    public ElephantLikeTrampleTask(ElephantLikeCombatProfile profile) : base(profile) { }

    // NondeterministicRandomFloat, not RandomFloat: this picks an animation clip and nothing else.
    // MBRandom's default stream is Game.Current.RandomGenerator — state on the saved Game root — so
    // a purely visual draw taken from it offsets every subsequent campaign roll. The damage roll in
    // the base task stays on the normal stream; only the clip choice moves.
    protected override ActionIndexCache GetAttackAction()
        => MBRandom.NondeterministicRandomFloat < 0.5f ? Profile.Trample : Profile.TrampleAlt;

    protected override void StampCooldown(DateTime now) => TrampleLastFired.SetValue(now);

    protected override ElephantLikeAttackKind AttackKind => ElephantLikeAttackKind.Trample;
}

/// <summary>
/// Left/right tusk swing — fired while the trample recharges, 4s cooldown. Picks the swing matching the best
/// enemy's bearing (written by <see cref="ElephantLikeEngageDecorator"/>): positive = LEFT, negative = RIGHT.
/// </summary>
public class ElephantLikeSideAttackTask : ElephantLikeAttackTaskBase
{
    public ElephantLikeSideAttackTask(ElephantLikeCombatProfile profile) : base(profile) { }

    protected override ActionIndexCache GetAttackAction()
        => TargetBearing.GetValue() >= 0f ? Profile.SwingLeft : Profile.SwingRight;

    protected override void StampCooldown(DateTime now) => SideAttackLastFired.SetValue(now);

    protected override ElephantLikeAttackKind AttackKind => ElephantLikeAttackKind.SideAttack;
}
