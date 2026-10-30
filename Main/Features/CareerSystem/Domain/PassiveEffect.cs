namespace TAOM.Features.CareerSystem.Domain;

public sealed class PassiveEffect
{
    public PassiveEffectType EffectType { get; }
    public float Magnitude { get; }

    // Hit-type gate for the combat passives whose consumer passes a hit (PassiveEffectConsumers.MaskGated:
    // Damage, Resistance, TroopDamage, TroopResistance). Honored on the per-hit damage path so a "+X% ranged
    // damage" pip only fires on ranged hits. Defaults to All (every hit). A type outside MaskGated never
    // reads it, so the parser keeps it at All there.
    public AttackTypeMask AttackTypeMask { get; }

    // Troop-class gate for the army-wide passives (PassiveEffectConsumers.ClassFiltered: TroopDamage,
    // TroopResistance, the three troop counters and TroopTraining): "+X% damage" for the leader's infantry only. Defaults to
    // All (every troop). A type outside ClassFiltered never reads it, so the parser keeps it at All there.
    public TroopClassMask TroopClass { get; }

    public PassiveEffect(
        PassiveEffectType effectType,
        float magnitude,
        AttackTypeMask attackTypeMask = AttackTypeMask.All,
        TroopClassMask troopClass = TroopClassMask.All)
    {
        EffectType = effectType;
        Magnitude = magnitude;
        AttackTypeMask = attackTypeMask;
        TroopClass = troopClass;
    }
}
