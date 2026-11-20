using System;
using System.Collections.Generic;

namespace TAOM.Features.Stamina.Domain;

/// <summary>What one people changes: a multiplier on the pool, one on regeneration, or no stamina at all.</summary>
public sealed class StaminaPeople
{
    public StaminaPeople(float pool, float regen, bool exempt)
    {
        Pool = pool;
        Regen = regen;
        Exempt = exempt;
    }

    public float Pool { get; }

    public float Regen { get; }

    /// <summary>This people does not tire: no pool, no cost, no lock (the Nazgul, Sauron, Saruman).</summary>
    public bool Exempt { get; }
}

/// <summary>
/// The Middle-earth part of the tuning (#1497): what a soldier's race and culture do to his pool and regeneration, and
/// what the weight of his armour does to his costs. Immutable. A race is keyed by its race name, a culture by its
/// culture id; a soldier in neither table has no modifier. The numbers are design choices with pointers to the books,
/// not quotations: the row list and its sources are in docs/features/stamina.md.
/// </summary>
public sealed class StaminaPeoples
{
    public const float DefaultMultiplierMin = 0.7f;
    public const float DefaultMultiplierMax = 1.6f;
    public const float DefaultArmourFreeKg = 10f;
    public const float DefaultArmourKgPerStep = 60f;
    public const float DefaultArmourFactorMax = 1.35f;

    private static readonly IReadOnlyDictionary<string, StaminaPeople> NoPeople =
        new Dictionary<string, StaminaPeople>(StringComparer.OrdinalIgnoreCase);

    /// <summary>No race or culture modifier, and the default armour rule.</summary>
    public static readonly StaminaPeoples None = new StaminaPeoples(NoPeople, NoPeople, DefaultMultiplierMin, DefaultMultiplierMax,
        DefaultArmourFreeKg, DefaultArmourKgPerStep, DefaultArmourFactorMax);

    public StaminaPeoples(IReadOnlyDictionary<string, StaminaPeople> races, IReadOnlyDictionary<string, StaminaPeople> cultures,
        float multiplierMin, float multiplierMax, float armourFreeKg, float armourKgPerStep, float armourFactorMax)
    {
        Races = races;
        Cultures = cultures;
        MultiplierMin = multiplierMin;
        MultiplierMax = multiplierMax;
        ArmourFreeKg = armourFreeKg;
        ArmourKgPerStep = armourKgPerStep;
        ArmourFactorMax = armourFactorMax;
    }

    public IReadOnlyDictionary<string, StaminaPeople> Races { get; }

    public IReadOnlyDictionary<string, StaminaPeople> Cultures { get; }

    /// <summary>The race and culture multipliers are multiplied, then kept inside these two values.</summary>
    public float MultiplierMin { get; }

    public float MultiplierMax { get; }

    /// <summary>Armour up to this weight costs nothing extra.</summary>
    public float ArmourFreeKg { get; }

    /// <summary>Each this many kilograms above the free weight adds 1 to the cost factor.</summary>
    public float ArmourKgPerStep { get; }

    public float ArmourFactorMax { get; }
}

/// <summary>One soldier's own stamina values, worked out once when the tick first looks at him.</summary>
public readonly struct StaminaProfile
{
    public StaminaProfile(float pool, float regenFactor, float costFactor, bool exempt)
    {
        Pool = pool;
        RegenFactor = regenFactor;
        CostFactor = costFactor;
        Exempt = exempt;
    }

    public float Pool { get; }

    public float RegenFactor { get; }

    public float CostFactor { get; }

    public bool Exempt { get; }
}
