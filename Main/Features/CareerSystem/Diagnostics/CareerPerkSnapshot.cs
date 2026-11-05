using System.Collections.Generic;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.Diagnostics;

/// <summary>
/// What <c>taom.career_perks</c> gathered at the boundary (#613): the hero's career, every
/// passive it holds, the campaign probes that carry a "Career" line, and the mission block when
/// a battle is running. Primitives only, so the report builder stays testable.
/// </summary>
public sealed class CareerPerkSnapshot
{
    public string? HeroId { get; set; }
    public string? HeroName { get; set; }
    public bool HasCareer { get; set; }
    public string? CareerId { get; set; }
    public int Level { get; set; }
    public List<CareerPerkRow> Rows { get; } = new List<CareerPerkRow>();
    public List<string> Probes { get; } = new List<string>();
    public CareerPerkMissionSnapshot? Mission { get; set; }
}

public sealed class CareerPerkRow
{
    public CareerPerkRow(PassiveEffectType type, float magnitude, List<(AttackTypeMask mask, float magnitude)>? masked)
    {
        Type = type;
        Magnitude = magnitude;
        Masked = masked;
    }

    public PassiveEffectType Type { get; }
    public float Magnitude { get; }
    /// <summary>Per-mask buckets for the Damage / Resistance types; null for the rest.</summary>
    public List<(AttackTypeMask mask, float magnitude)>? Masked { get; }
}

public sealed class CareerPerkMissionSnapshot
{
    public bool PlayerAgentAlive { get; set; }
    public float SwingSpeedMultiplier { get; set; }
    public float MaxSpeedMultiplier { get; set; }
    public float DamageMultiplierBonus { get; set; }
    public float ReadySpeedMultiplier { get; set; }
    public float ArmorEncumbrance { get; set; }
    public bool HasMount { get; set; }
    public float MountChargeDamage { get; set; }
    public float MountSpeed { get; set; }
    public float MountHealthLimit { get; set; }
    public List<string> AmmoSlots { get; } = new List<string>();
    public string? LiveBuff { get; set; }
}
