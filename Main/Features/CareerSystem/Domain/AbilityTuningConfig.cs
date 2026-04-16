namespace TAOM.Features.CareerSystem.Domain;

public sealed class AbilityTuningConfig
{
    public InfantryTuning Infantry { get; }
    public RangedTuning Ranged { get; }
    public CavalryTuning Cavalry { get; }

    public AbilityTuningConfig(InfantryTuning infantry, RangedTuning ranged, CavalryTuning cavalry)
    {
        Infantry = infantry;
        Ranged = ranged;
        Cavalry = cavalry;
    }

    public static AbilityTuningConfig Default => new AbilityTuningConfig(
        InfantryTuning.Default, RangedTuning.Default, CavalryTuning.Default);
}

public sealed class InfantryTuning
{
    public float DamageBonus { get; }
    public float DamageReduction { get; }
    public float Radius { get; }

    public InfantryTuning(float damageBonus, float damageReduction, float radius)
    {
        DamageBonus = damageBonus;
        DamageReduction = damageReduction;
        Radius = radius;
    }

    public static InfantryTuning Default => new InfantryTuning(15f, 10f, 50f);
}

public sealed class RangedTuning
{
    public float SpeedBonus { get; }
    public float RangedDamageBonus { get; }
    public float DrawSpeedBonus { get; }

    public RangedTuning(float speedBonus, float rangedDamageBonus, float drawSpeedBonus)
    {
        SpeedBonus = speedBonus;
        RangedDamageBonus = rangedDamageBonus;
        DrawSpeedBonus = drawSpeedBonus;
    }

    public static RangedTuning Default => new RangedTuning(15f, 20f, 20f);
}

public sealed class CavalryTuning
{
    public float MountSpeedBonus { get; }
    public float ChargeDamageBonus { get; }
    public float DamageBonus { get; }

    public CavalryTuning(float mountSpeedBonus, float chargeDamageBonus, float damageBonus)
    {
        MountSpeedBonus = mountSpeedBonus;
        ChargeDamageBonus = chargeDamageBonus;
        DamageBonus = damageBonus;
    }

    public static CavalryTuning Default => new CavalryTuning(20f, 25f, 10f);
}
