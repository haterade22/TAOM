using System.Collections.Generic;
using Newtonsoft.Json;

namespace TAOM.Features.RaceAbilities.Domain;

// race_abilities.json, as Json.NET reads it. RaceAbilitiesConfigProvider validates every field before
// anything reads it (csharp-architecture.md "Config Providers MUST Validate"); a missing or broken file
// gives these compiled defaults, which the shipped JSON mirrors (ShippedRaceAbilitiesConfigTests).
public class RaceAbilitiesConfig
{
    public bool Enabled { get; set; } = true;

    public RaceAbilityTierScaling TierScaling { get; set; } = new RaceAbilityTierScaling();

    // Keyed by race name as skins.xml spells it ("dwarf"); one key may list several races, comma-separated,
    // which then share one profile. Validated against the engine's race registry lazily by
    // RaceAbilityProfileResolver: the registry is engine state, empty when this file loads.
    public Dictionary<string, RaceAbilityProfile> Races { get; set; } = RaceAbilityDefaults.RaceProfiles();

    // Keyed by culture id ("gondor", "vlandia" for Rohan), comma-separated aliases allowed. Applies only to
    // soldiers of the human race, and only when their race has no profile of its own.
    public Dictionary<string, RaceAbilityProfile> Cultures { get; set; } = RaceAbilityDefaults.CultureProfiles();
}

// How strongly a soldier's ability hits, by his battle tier (BasicCharacterObject.GetBattleTier: 0 to 7,
// heroes 7). A hero takes HeroFactor instead of the tier rule.
public class RaceAbilityTierScaling
{
    public int BaseTier { get; set; } = 3;

    public float PercentPerTier { get; set; } = 5f;

    public float MinFactor { get; set; } = 0.85f;

    public float MaxFactor { get; set; } = 1.2f;

    public float HeroFactor { get; set; } = 1.25f;
}

// One race's or culture's ability. Cooldown runs from activation. The ability fires when every Requires
// trigger holds and, if AnyOf is not empty, at least one AnyOf trigger holds.
public class RaceAbilityProfile
{
    // Names the ability in logs and picks its display name (RaceAbilityNames).
    public string AbilityId { get; set; } = "";

    public float CooldownSeconds { get; set; }

    public float DurationSeconds { get; set; }

    // The aftermath: Spent effects apply for this long once the ability ends (0 = none).
    public float SpentSeconds { get; set; }

    // Each kill while active adds this much, up to MaxDurationSeconds from activation (0 = no extension).
    public float KillExtensionSeconds { get; set; }

    public float MaxDurationSeconds { get; set; }

    // Kin of the same profile and team within this radius whose ability is ready fire with the one that
    // fired first (0 = alone).
    public float RallyRadius { get; set; }

    // The voice line played on activation: "", "Yell", "Charge", "Victory" or "Grunt".
    public string WarCry { get; set; } = "";

    // Races that count as kin for the KinWithin and KinFell triggers and the kin bonus, besides soldiers of
    // the same profile (orcs and goblins swarm together). Never widens the rally: only the same ability rallies.
    public List<string> KinRaces { get; set; } = new List<string>();

    // Extra melee damage for every kinsman close by at activation (the orcs' Swarm); null for none.
    public RaceAbilityKinBonus? KinBonus { get; set; }

    public List<RaceAbilityTrigger> Requires { get; set; } = new List<RaceAbilityTrigger>();

    public List<RaceAbilityTrigger> AnyOf { get; set; } = new List<RaceAbilityTrigger>();

    public RaceAbilityEffects Effects { get; set; } = new RaceAbilityEffects();

    public RaceAbilityEffects Spent { get; set; } = new RaceAbilityEffects();
}

public class RaceAbilityKinBonus
{
    public float Radius { get; set; }

    public float PerKinPercent { get; set; }

    public int MaxKin { get; set; }
}

public class RaceAbilityTrigger
{
    // A RaceAbilityTriggerKind name, matched case-insensitively.
    public string Kind { get; set; } = "";

    // Metres, for the kinds that look around the soldier.
    public float Range { get; set; }

    // How recent an event must be (LandedKill, KinFell).
    public float Seconds { get; set; }

    // A fraction, 0 to 1: health (HealthBelow, WoundedEnemyWithin) or morale over 100 (MoraleBelow).
    public float Fraction { get; set; }

    // How many (EnemiesWithin, KinWithin).
    public int Count { get; set; }

    // Set by the provider from Kind; never read from the file.
    [JsonIgnore]
    public RaceAbilityTriggerKind ParsedKind { get; set; }
}

public enum RaceAbilityTriggerKind
{
    Always,
    EnemyWithin,
    EnemiesWithin,
    HealthBelow,
    TookDamage,
    KinFell,
    LandedKill,
    WoundedEnemyWithin,
    CavalryClosing,
    RangedTargetWithin,
    KinWithin,
    MoraleBelow,
    NoEnemyWithin,
    Mounted,
}

// What an ability does while it lasts. Percentages are whole numbers (20 = +20%, -60 = -60%); the
// appliers divide by 100. Every stat field here is one the engine rewrites from scratch on each
// UpdateAgentStats in both the campaign and the Custom Battle model (docs/features/race-abilities.md,
// "Engine levers"), so a multiply never compounds.
public class RaceAbilityEffects
{
    // On foot only: a rider moves at his horse's speed (MountSpeedPercent).
    public float MoveSpeedPercent { get; set; }

    public float AccelerationPercent { get; set; }

    public float SwingSpeedPercent { get; set; }

    // ThrustOrRangedReadySpeedMultiplier: bow draw, throw and thrust readying.
    public float DrawSpeedPercent { get; set; }

    public float ReloadSpeedPercent { get; set; }

    public float MissileSpeedPercent { get; set; }

    // The ridden horse's MountSpeed, written on the horse's own stats through its rider.
    public float MountSpeedPercent { get; set; }

    // Melee hits only, applied in the damage model (Custom Battle reads no driven-property damage bonus).
    public float MeleeDamagePercent { get; set; }

    // Missile hits the soldier lands, applied in the damage model.
    public float RangedDamagePercent { get; set; }

    // Every hit the soldier takes (not his horse), applied in the damage model.
    public float DamageReductionPercent { get; set; }

    public float KnockdownResistancePercent { get; set; }

    // Read by the engine for missiles, crush-throughs and wide-grip thrusts only: a frontal horse charge, a
    // kick and a shield bash knock back regardless.
    public float KnockbackResistancePercent { get; set; }

    public float DismountResistancePercent { get; set; }

    public float BlockAbilityPercent { get; set; }

    public float ParryAbilityPercent { get; set; }

    public float AttackEagernessPercent { get; set; }

    // AiShooterError: negative is better aim.
    public float AimErrorPercent { get; set; }

    // Every melee swing breaks a block, unless the defender holds against crush-through.
    public bool ForceCrushThrough { get; set; }

    public bool HoldAgainstCrush { get; set; }

    // On a weapon or missile hit the soldier does not flinch (his attack is not interrupted) and is spared
    // knockdown, knock-back and dismount, as vanilla's shrug-off does; the attacker's weapon bounces. A horse
    // charge never asks, and a kick or shield bash still knocks back.
    public bool ShrugOffBlows { get; set; }

    // While the effect lasts the soldier cannot panic, and his morale is topped up to this (0 = none).
    public float MoraleFloor { get; set; }

    // Morale added (or, negative, taken) when the ability ends: the price of a frenzy that burns out.
    public float MoraleOnEnd { get; set; }

    public float HealPerKill { get; set; }

    public float FearOnKillRadius { get; set; }

    public float FearOnKillMorale { get; set; }

    // Enemies within the radius lose morale every second while the effect lasts; where several auras reach
    // one enemy, only the strongest counts.
    public float FearAuraRadius { get; set; }

    public float FearAuraMoralePerSecond { get; set; }
}
