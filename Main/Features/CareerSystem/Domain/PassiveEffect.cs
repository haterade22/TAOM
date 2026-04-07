namespace TAOM.Features.CareerSystem.Domain;

public sealed class PassiveEffect
{
    public PassiveEffectType EffectType { get; }
    public float Magnitude { get; }
    public OperationType Operation { get; }
    public bool IsPercentage { get; }
    public AttackTypeMask AttackTypeMask { get; }

    public PassiveEffect(
        PassiveEffectType effectType,
        float magnitude,
        OperationType operation = OperationType.Add,
        bool isPercentage = false,
        AttackTypeMask attackTypeMask = AttackTypeMask.All)
    {
        EffectType = effectType;
        Magnitude = magnitude;
        Operation = operation;
        IsPercentage = isPercentage;
        AttackTypeMask = attackTypeMask;
    }
}
