using System.Collections.Generic;
using Newtonsoft.Json;
using TAOM.Features.RaceAbilities.Domain;

namespace TAOM.Features.HeroAbilities.Domain;

/// <summary>hero_abilities/hero_abilities.json (#1359, docs/features/hero-abilities.md).</summary>
public sealed class HeroAbilitiesConfig
{
    public bool Enabled { get; set; } = true;

    public List<HeroKitConfig> Heroes { get; set; } = new List<HeroKitConfig>();
}

/// <summary>One hero's kit, keyed by the character id (taom_hero_aragorn): a passive and timed abilities.</summary>
public sealed class HeroKitConfig
{
    public string CharacterId { get; set; } = "";

    // "#RRGGBB": the outline while one of his abilities runs ("" = none).
    public string Glow { get; set; } = "";

    // Set by the provider from Glow, opaque 0xFFRRGGBB; never read from the file.
    [JsonIgnore]
    public uint? GlowColor { get; set; }

    // Granted to the hero once, for the whole mission.
    public RaceAbilityEffects Passive { get; set; } = new RaceAbilityEffects();

    // The ranged part (aim, shield pierce, arrow kill, extra arrows); null for a kit without one.
    public HeroRangedConfig? Ranged { get; set; }

    public List<HeroAbilityConfig> Abilities { get; set; } = new List<HeroAbilityConfig>();
}

/// <summary>
/// One timed ability. It fires like a race ability (every Requires trigger holds and one AnyOf does), gives its
/// allies in Radius the AllyEffects, the hero both effect sets, and shocks the enemies in EnemyShockRadius.
/// </summary>
public sealed class HeroAbilityConfig
{
    public string Id { get; set; } = "";

    public float CooldownSeconds { get; set; }

    public float DurationSeconds { get; set; }

    public List<RaceAbilityTrigger> Requires { get; set; } = new List<RaceAbilityTrigger>();

    public List<RaceAbilityTrigger> AnyOf { get; set; } = new List<RaceAbilityTrigger>();

    // Metres around the hero, at most 15 (the engine's proximity query limit).
    public float Radius { get; set; }

    public RaceAbilityEffects AllyEffects { get; set; } = new RaceAbilityEffects();

    public RaceAbilityEffects SelfEffects { get; set; } = new RaceAbilityEffects();

    // Morale each ally gains at the cast.
    public float AllyMorale { get; set; }

    public float EnemyShockRadius { get; set; }

    // The fear morale handed to MoraleShock for each enemy in reach.
    public float EnemyShockMorale { get; set; }

    // "", "Yell", "Charge", "Victory" or "Grunt".
    public string WarCry { get; set; } = "";

    // Extra arrows per shot while the ability runs, 0 to 3; needs a usable Ranged block on the kit.
    public int ExtraArrows { get; set; }

    // The ally effects reach only allies that carry a ranged weapon.
    public bool AlliesRangedOnly { get; set; }
}
