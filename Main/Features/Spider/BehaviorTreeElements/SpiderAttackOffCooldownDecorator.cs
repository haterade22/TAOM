using System;
using BehaviorTrees;

namespace TAOM.Features.Spider.BehaviorTreeElements;

/// <summary>
/// Cooldown gate for one spider attack kind: passes when the attack has never fired or its cooldown window has
/// fully elapsed. The pure check is <see cref="ISpiderAttackService.IsOffCooldown"/>; the stamps live on the tree
/// blackboard and are written by the attack tasks when they fire. One class, two instances in the tree
/// (Pounce — priority, long cooldown; SideAttack — gap-filler, short cooldown). Mirrors the elephant's
/// AttackOffCooldownDecorator.
/// </summary>
public class SpiderAttackOffCooldownDecorator : BTReturnFalseDecorator, IBTSpiderBlackboard
{
    private readonly SpiderAttackKind _kind;
    private readonly double _cooldownSeconds;
    private ISpiderAttackService _service;

    private BTBlackboardValue<DateTime?> _pounceLastFired;
    private BTBlackboardValue<DateTime?> _sideAttackLastFired;
    private BTBlackboardValue<float> _targetBearing;
    public BTBlackboardValue<DateTime?> PounceLastFired { get => _pounceLastFired; set => _pounceLastFired = value; }
    public BTBlackboardValue<DateTime?> SideAttackLastFired { get => _sideAttackLastFired; set => _sideAttackLastFired = value; }
    public BTBlackboardValue<float> TargetBearing { get => _targetBearing; set => _targetBearing = value; }

    public SpiderAttackOffCooldownDecorator(SpiderAttackKind kind, double cooldownSeconds)
    {
        _kind = kind;
        _cooldownSeconds = cooldownSeconds;
    }

    public override bool Evaluate()
    {
        var stamp = _kind == SpiderAttackKind.Pounce ? PounceLastFired : SideAttackLastFired;
        _service ??= IoC.Resolve<ISpiderAttackService>();
        return _service.IsOffCooldown(stamp.GetValue(), DateTime.Now, _cooldownSeconds);
    }
}
