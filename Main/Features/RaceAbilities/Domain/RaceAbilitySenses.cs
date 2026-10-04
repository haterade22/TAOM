using System.Collections.Generic;

namespace TAOM.Features.RaceAbilities.Domain;

// What one soldier perceives at one decision, gathered by the sensor from the engine and read by
// RaceAbilityService. One instance, owned by RaceAbilitySensor, cleared and refilled each decision, so a
// decision allocates nothing once the lists have grown.
public sealed class RaceAbilitySenses
{
    public float Now { get; set; }

    // Health over HealthLimit, 0 to 1.
    public float HealthFraction { get; set; } = 1f;

    // 0 to 100; -1 when the soldier has no morale component (the player).
    public float Morale { get; set; } = -1f;

    // Lost health since the previous decision (about a second ago).
    public bool TookDamage { get; set; }

    public bool WieldsRanged { get; set; }

    public bool Mounted { get; set; }

    public float? LastKillAt { get; set; }

    public List<EnemySense> Enemies { get; } = new List<EnemySense>();

    public List<float> KinDistances { get; } = new List<float>();

    public List<FallenSense> FallenKin { get; } = new List<FallenSense>();

    public void Clear()
    {
        Now = 0f;
        HealthFraction = 1f;
        Morale = -1f;
        TookDamage = false;
        WieldsRanged = false;
        Mounted = false;
        LastKillAt = null;
        Enemies.Clear();
        KinDistances.Clear();
        FallenKin.Clear();
    }
}

public readonly struct EnemySense
{
    public EnemySense(float distance, float healthFraction, bool cavalryClosing)
    {
        Distance = distance;
        HealthFraction = healthFraction;
        CavalryClosing = cavalryClosing;
    }

    public float Distance { get; }

    public float HealthFraction { get; }

    // Mounted and riding towards the soldier.
    public bool CavalryClosing { get; }
}

public readonly struct FallenSense
{
    public FallenSense(float distance, float at)
    {
        Distance = distance;
        At = at;
    }

    public float Distance { get; }

    // Mission time of the death.
    public float At { get; }
}
