using System.Collections.Generic;
using TAOM.Features.RaceAbilities.Domain;

namespace TAOM.Features.HeroAuras.Domain;

/// <summary>hero_auras/hero_auras.json (#1127, docs/features/hero-auras.md).</summary>
public sealed class HeroAurasConfig
{
    public bool Enabled { get; set; } = true;

    // How often each hero's aura looks for the allies in reach.
    public float PulseSeconds { get; set; } = 1f;

    // How long a soldier keeps the aura after he leaves its reach or the hero falls.
    public float LingerSeconds { get; set; } = 3f;

    // The reach of an aura that names none. At most 15 m: past that the engine's nearby-agent query walks every agent.
    public float Radius { get; set; } = 15f;

    public List<HeroAuraDefinition> Auras { get; set; } = new List<HeroAuraDefinition>();
}

/// <summary>One hero's aura: who carries it (hero StringIds), who it reaches and what it gives.</summary>
public sealed class HeroAuraDefinition
{
    public string Id { get; set; } = "";

    // Hero StringIds; in a battle without a campaign, the character id (DreadSourceTracker.HeroAxisId).
    public List<string> HeroIds { get; set; } = new List<string>();

    // Null: the config's radius.
    public float? Radius { get; set; }

    // Race names the aura reaches; empty: every allied soldier.
    public List<string> Races { get; set; } = new List<string>();

    // The race abilities' effect channels, read through RaceAbilityHooks.
    public RaceAbilityEffects Effects { get; set; } = new RaceAbilityEffects();
}
