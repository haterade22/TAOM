using System;
using BehaviorTrees;

namespace TAOM.Features.Elephant.BehaviorTreeElements;

/// <summary>Which elephant attack a cooldown decorator gates.</summary>
public enum ElephantAttackKind
{
    Trample,
    SideAttack,
}

/// <summary>
/// Cooldown gate for one elephant attack kind: passes when the attack has never fired or its cooldown window has
/// fully elapsed. The pure check is <see cref="IElephantAttackService.IsOffCooldown"/>; the stamps live on the
/// tree blackboard and are written by the attack tasks when they fire. One class, two instances in the tree
/// (Trample 10s priority branch, SideAttack 4s fallback branch).
/// </summary>
public class AttackOffCooldownDecorator : BTReturnFalseDecorator, IBTElephantBlackboard
{
    private readonly ElephantAttackKind _kind;
    private readonly double _cooldownSeconds;
    private IElephantAttackService _service;

    private BTBlackboardValue<DateTime?> _trampleLastFired;
    private BTBlackboardValue<DateTime?> _sideAttackLastFired;
    private BTBlackboardValue<float> _targetBearing;
    public BTBlackboardValue<DateTime?> TrampleLastFired { get => _trampleLastFired; set => _trampleLastFired = value; }
    public BTBlackboardValue<DateTime?> SideAttackLastFired { get => _sideAttackLastFired; set => _sideAttackLastFired = value; }
    public BTBlackboardValue<float> TargetBearing { get => _targetBearing; set => _targetBearing = value; }

    public AttackOffCooldownDecorator(ElephantAttackKind kind, double cooldownSeconds)
    {
        _kind = kind;
        _cooldownSeconds = cooldownSeconds;
    }

    public override bool Evaluate()
    {
        var stamp = _kind == ElephantAttackKind.Trample ? TrampleLastFired : SideAttackLastFired;
        _service ??= IoC.Resolve<IElephantAttackService>();
        return _service.IsOffCooldown(stamp.GetValue(), DateTime.Now, _cooldownSeconds);
    }
}
