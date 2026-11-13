using System;
using System.Collections.Generic;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.HeroAuras.Domain;
using TAOM.Features.RaceAbilities;
using TAOM.Features.RaceAbilities.Domain;

namespace TAOM.Features.HeroAuras;

/// <summary>
/// The hero auras' rules (#1127, docs/features/hero-auras.md): which hero carries which aura, by hero StringId only,
/// who it reaches, its effects at the MCM strength, and the merge of several auras on one soldier. The lookup is built
/// on first use, because race names need the engine's registry, which is empty when the config loads. Pure apart from
/// that registry, behind <see cref="IRaceManager"/>.
/// </summary>
public sealed class HeroAuraService
{
    private readonly IHeroAurasConfigProvider _config;
    private readonly IRaceManager _races;
    private readonly RaceAbilityService _raceAbilities;
    private readonly IModLogger _logger;
    private volatile Dictionary<string, HeroAura>? _byHero;

    public HeroAuraService(IHeroAurasConfigProvider config, IRaceManager races, RaceAbilityService raceAbilities, IModLogger logger)
    {
        _config = config;
        _races = races;
        _raceAbilities = raceAbilities;
        _logger = logger;
    }

    public bool ConfigEnabled => _config.GetConfig().Enabled;

    public float PulseSeconds => _config.GetConfig().PulseSeconds;

    public float LingerSeconds => _config.GetConfig().LingerSeconds;

    public HeroAura? AuraFor(string? heroId)
    {
        if (string.IsNullOrEmpty(heroId))
            return null;
        var map = _byHero ?? Build();
        return map.TryGetValue(heroId!, out var aura) ? aura : null;
    }

    // The aura's effects at this strength (1 = as authored), clamped as a race ability's are.
    public RaceAbilityEffects EffectsFor(HeroAura aura, float strength)
    {
        if (aura.CachedEffects != null && aura.CachedStrength.Equals(strength))
            return aura.CachedEffects;
        var effects = _raceAbilities.Scale(aura.Effects, strength);
        aura.CachedEffects = effects;
        aura.CachedStrength = strength;
        return effects;
    }

    // Several auras on one soldier: per channel the strongest counts (RaceAbilityEffectsMerge).
    public RaceAbilityEffects Merge(IReadOnlyList<RaceAbilityEffects> auras)
    {
        var merged = auras[0];
        for (var i = 1; i < auras.Count; i++)
            merged = RaceAbilityEffectsMerge.Combine(merged, auras[i])!;
        return merged;
    }

    private Dictionary<string, HeroAura> Build()
    {
        var config = _config.GetConfig();
        var map = new Dictionary<string, HeroAura>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in config.Auras)
        {
            var aura = new HeroAura(definition.Id, definition.Radius ?? config.Radius, RaceIds(definition), definition.Effects);
            foreach (var heroId in definition.HeroIds)
                map[heroId] = aura;
        }
        _byHero = map;
        return map;
    }

    private HashSet<int>? RaceIds(HeroAuraDefinition definition)
    {
        if (definition.Races.Count == 0)
            return null;
        var ids = new HashSet<int>();
        foreach (var name in definition.Races)
        {
            if (_races.IsValidRaceName(name))
                ids.Add(_races.GetRaceIdFromName(name));
            else
                _logger.LogWarning($"[HeroAuras] aura '{definition.Id}': race '{name}' is not a known race and is skipped");
        }
        return ids;
    }
}
