namespace TAOM.Features.CareerSystem.Domain;

public sealed class AbilityTuningConfig
{
    public GlobalTuning Global { get; }
    public InfantryTuning Infantry { get; }
    public RangedTuning Ranged { get; }
    public CavalryTuning Cavalry { get; }

    public AbilityTuningConfig(GlobalTuning global, InfantryTuning infantry, RangedTuning ranged, CavalryTuning cavalry)
    {
        Global = global;
        Infantry = infantry;
        Ranged = ranged;
        Cavalry = cavalry;
    }

    public static AbilityTuningConfig Default => new AbilityTuningConfig(
        GlobalTuning.Default, InfantryTuning.Default, RangedTuning.Default, CavalryTuning.Default);
}

public sealed class GlobalTuning
{
    public float CooldownSeconds { get; }

    // Issue #104 Option B — minimum effective cooldown after CooldownReduction mutations.
    // Floor prevents designer-stack-up cheese (50 careers × multiple keystones could in theory
    // sum to a 30s+ reduction; clamp ensures the global cooldown is still observed at minimum 5s).
    public float MinCooldownSeconds { get; }

    public GlobalTuning(float cooldownSeconds, float minCooldownSeconds = 5f)
    {
        CooldownSeconds = cooldownSeconds;
        MinCooldownSeconds = minCooldownSeconds;
    }

    public static GlobalTuning Default => new GlobalTuning(30f, 5f);
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
