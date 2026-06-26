namespace TAOM.Features.CareerSystem.Domain;

public sealed class PassiveEffect
{
    public PassiveEffectType EffectType { get; }
    public float Magnitude { get; }

    // Delivery-type gate for combat passives (Damage / Resistance). Honored on the per-hit damage
    // path so a "+X% ranged damage" pip only fires on ranged hits. Defaults to All (every hit).
    public AttackTypeMask AttackTypeMask { get; }

    public PassiveEffect(
        PassiveEffectType effectType,
        float magnitude,
        AttackTypeMask attackTypeMask = AttackTypeMask.All)
    {
        EffectType = effectType;
        Magnitude = magnitude;
        AttackTypeMask = attackTypeMask;
    }
}
