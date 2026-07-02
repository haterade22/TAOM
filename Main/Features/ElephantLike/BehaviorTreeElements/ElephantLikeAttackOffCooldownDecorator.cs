using System;
using BehaviorTrees;

namespace TAOM.Features.ElephantLike.BehaviorTreeElements;

/// <summary>
/// Cooldown gate for one elephant-like attack kind: passes when the attack has never fired or its cooldown window
/// has fully elapsed. The pure check is <see cref="IElephantLikeAttackService.IsOffCooldown"/>; the stamps live on
/// the tree blackboard and are written by the attack tasks when they fire. One class, two instances per tree
/// (Trample 10s priority branch, SideAttack 4s fallback branch).
/// </summary>
public class ElephantLikeAttackOffCooldownDecorator : BTReturnFalseDecorator, IBTElephantLikeBlackboard
{
    private readonly ElephantLikeCombatProfile _profile;
    private readonly ElephantLikeAttackKind _kind;
    private readonly double _cooldownSeconds;
    private IElephantLikeAttackService _service;

    private BTBlackboardValue<DateTime?> _trampleLastFired;
    private BTBlackboardValue<DateTime?> _sideAttackLastFired;
    private BTBlackboardValue<float> _targetBearing;
    public BTBlackboardValue<DateTime?> TrampleLastFired { get => _trampleLastFired; set => _trampleLastFired = value; }
    public BTBlackboardValue<DateTime?> SideAttackLastFired { get => _sideAttackLastFired; set => _sideAttackLastFired = value; }
    public BTBlackboardValue<float> TargetBearing { get => _targetBearing; set => _targetBearing = value; }

    public ElephantLikeAttackOffCooldownDecorator(ElephantLikeCombatProfile profile, ElephantLikeAttackKind kind, double cooldownSeconds)
    {
        _profile = profile;
        _kind = kind;
        _cooldownSeconds = cooldownSeconds;
    }

    public override bool Evaluate()
    {
        var stamp = _kind == ElephantLikeAttackKind.Trample ? TrampleLastFired : SideAttackLastFired;
        _service ??= _profile.ResolveService();
        return _service.IsOffCooldown(stamp.GetValue(), DateTime.Now, _cooldownSeconds);
    }
}
