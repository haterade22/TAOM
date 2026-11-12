using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.HeroAuras.Domain;
using TAOM.Features.RaceAbilities.Domain;

namespace TAOM.Features.HeroAuras;

/// <summary>
/// Validating loader for <c>hero_auras/hero_auras.json</c> (DreadAuraConfigProvider pattern): a missing file gives
/// defaults and a warning, a parse failure defaults and an error, an invalid value its default and a warning. An aura
/// with no id or no hero is skipped; a hero already carrying an aura is dropped from the later one. An aura only
/// strengthens, so a harmful value (a negative bonus, a positive aim error) is zeroed, and so is a field only a race
/// ability reads. Race names and hero ids are not checked here: race names need the engine registry
/// (<see cref="HeroAuraService"/> checks them), hero ids are pinned by ShippedHeroAurasConfigTests.
/// </summary>
public sealed class HeroAurasConfigProvider : IHeroAurasConfigProvider
{
    // Mission.GetNearbyAllyAgents searches the proximity grid up to 15 m and walks every agent past it.
    private const float MaxRadius = 15f;
    private const float MaxMoraleFloor = 50f;

    private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
    {
        ObjectCreationHandling = ObjectCreationHandling.Replace,
    };

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<HeroAurasConfig> _config;
    private bool _rejected;

    public HeroAurasConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _config = new Lazy<HeroAurasConfig>(Load);
    }

    public HeroAurasConfig GetConfig() => _config.Value;

    private HeroAurasConfig Load()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "hero_auras", "hero_auras.json");
        if (!File.Exists(path))
        {
            _logger.LogWarning($"HeroAurasConfigProvider: hero_auras.json not found at {path}, no hero carries an aura");
            return new HeroAurasConfig();
        }

        HeroAurasConfig parsed;
        try
        {
            parsed = JsonConvert.DeserializeObject<HeroAurasConfig>(File.ReadAllText(path), SerializerSettings) ?? new HeroAurasConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError($"HeroAurasConfigProvider: failed to parse hero_auras.json: {ex.Message}");
            return new HeroAurasConfig();
        }

        var config = Validate(parsed);
        _logger.LogInfo($"HeroAurasConfigProvider: loaded hero_auras.json, {config.Auras.Count} aura(s)");
        return config;
    }

    private HeroAurasConfig Validate(HeroAurasConfig parsed)
    {
        var defaults = new HeroAurasConfig();
        var config = new HeroAurasConfig
        {
            Enabled = parsed.Enabled,
            PulseSeconds = InRange(parsed.PulseSeconds, defaults.PulseSeconds, 0.25f, 5f, "pulseSeconds"),
            LingerSeconds = InRange(parsed.LingerSeconds, defaults.LingerSeconds, 0.25f, 10f, "lingerSeconds"),
            Radius = InRange(parsed.Radius, defaults.Radius, 1f, MaxRadius, "radius"),
        };

        var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var auraIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var aura in parsed.Auras ?? new List<HeroAuraDefinition>())
        {
            var checkedAura = ValidateAura(aura, claimed, auraIds);
            if (checkedAura != null)
                config.Auras.Add(checkedAura);
        }

        if (_rejected)
            _logger.LogWarning("HeroAurasConfigProvider: hero_auras.json contained invalid values. See prior warnings for details.");
        return config;
    }

    private HeroAuraDefinition? ValidateAura(HeroAuraDefinition? aura, HashSet<string> claimed, HashSet<string> auraIds)
    {
        var id = aura?.Id?.Trim();
        if (aura == null || string.IsNullOrEmpty(id))
        {
            Reject("an aura has no id and was skipped");
            return null;
        }
        // A soldier's aura key is built from ids joined by '+' and ended by '@' (HeroAura.KeyFor): an id must be
        // unique and free of both, or two sets of auras could share a key and a soldier keep stale effects.
        if (id!.IndexOf('+') >= 0 || id.IndexOf('@') >= 0)
        {
            Reject($"aura '{id}' has '+' or '@' in its id and was skipped");
            return null;
        }
        if (!auraIds.Add(id))
        {
            Reject($"aura '{id}' repeats an earlier aura's id and was skipped");
            return null;
        }

        var heroIds = new List<string>();
        foreach (var raw in aura.HeroIds ?? new List<string>())
        {
            var heroId = raw?.Trim();
            if (string.IsNullOrEmpty(heroId))
                continue;
            if (claimed.Add(heroId!))
                heroIds.Add(heroId!);
            else
                Reject($"aura '{id}': hero '{heroId}' already carries an earlier aura and was dropped from this one");
        }
        if (heroIds.Count == 0)
        {
            Reject($"aura '{id}' names no hero and was skipped");
            return null;
        }

        float? radius = aura.Radius;
        if (radius.HasValue && !(FiniteFloatValidator.IsFinite(radius.Value) && radius.Value >= 1f && radius.Value <= MaxRadius))
        {
            Reject($"aura '{id}': radius {radius} is outside 1 to {MaxRadius} m, using the default");
            radius = null;
        }

        var races = new List<string>();
        foreach (var race in aura.Races ?? new List<string>())
            if (!string.IsNullOrWhiteSpace(race))
                races.Add(race.Trim());

        return new HeroAuraDefinition
        {
            Id = id!, HeroIds = heroIds, Radius = radius, Races = races,
            Effects = ValidateEffects(id!, aura.Effects ?? new RaceAbilityEffects()),
        };
    }

    private RaceAbilityEffects ValidateEffects(string id, RaceAbilityEffects e)
    {
        float Gain(float value, string name) => Check(id, name, value, value >= 0f);
        return new RaceAbilityEffects
        {
            MoveSpeedPercent = Gain(e.MoveSpeedPercent, "moveSpeedPercent"),
            AccelerationPercent = Gain(e.AccelerationPercent, "accelerationPercent"),
            SwingSpeedPercent = Gain(e.SwingSpeedPercent, "swingSpeedPercent"),
            DrawSpeedPercent = Gain(e.DrawSpeedPercent, "drawSpeedPercent"),
            ReloadSpeedPercent = Gain(e.ReloadSpeedPercent, "reloadSpeedPercent"),
            MissileSpeedPercent = Gain(e.MissileSpeedPercent, "missileSpeedPercent"),
            MountSpeedPercent = Gain(e.MountSpeedPercent, "mountSpeedPercent"),
            MeleeDamagePercent = Gain(e.MeleeDamagePercent, "meleeDamagePercent"),
            RangedDamagePercent = Gain(e.RangedDamagePercent, "rangedDamagePercent"),
            DamageReductionPercent = Gain(e.DamageReductionPercent, "damageReductionPercent"),
            KnockdownResistancePercent = Gain(e.KnockdownResistancePercent, "knockdownResistancePercent"),
            KnockbackResistancePercent = Gain(e.KnockbackResistancePercent, "knockbackResistancePercent"),
            DismountResistancePercent = Gain(e.DismountResistancePercent, "dismountResistancePercent"),
            BlockAbilityPercent = Gain(e.BlockAbilityPercent, "blockAbilityPercent"),
            ParryAbilityPercent = Gain(e.ParryAbilityPercent, "parryAbilityPercent"),
            AttackEagernessPercent = Gain(e.AttackEagernessPercent, "attackEagernessPercent"),
            AimErrorPercent = Check(id, "aimErrorPercent", e.AimErrorPercent, e.AimErrorPercent <= 0f),
            ForceCrushThrough = e.ForceCrushThrough,
            HoldAgainstCrush = e.HoldAgainstCrush,
            ShrugOffBlows = e.ShrugOffBlows,
            MoraleFloor = Math.Min(MaxMoraleFloor, Gain(e.MoraleFloor, "moraleFloor")),
            MoraleOnEnd = AbilityOnly(id, "moraleOnEnd", e.MoraleOnEnd),
            HealPerKill = AbilityOnly(id, "healPerKill", e.HealPerKill),
            FearOnKillRadius = AbilityOnly(id, "fearOnKillRadius", e.FearOnKillRadius),
            FearOnKillMorale = AbilityOnly(id, "fearOnKillMorale", e.FearOnKillMorale),
            FearAuraRadius = AbilityOnly(id, "fearAuraRadius", e.FearAuraRadius),
            FearAuraMoralePerSecond = AbilityOnly(id, "fearAuraMoralePerSecond", e.FearAuraMoralePerSecond),
        };
    }

    private float Check(string id, string name, float value, bool strengthens)
    {
        if (FiniteFloatValidator.IsFinite(value) && strengthens)
            return value;
        Reject($"aura '{id}': {name} {value} would weaken the soldiers, set to 0");
        return 0f;
    }

    private float AbilityOnly(string id, string name, float value)
    {
        if (value != 0f)
            Reject($"aura '{id}': {name} is read only for a race ability, set to 0");
        return 0f;
    }

    private float InRange(float value, float fallback, float min, float max, string name)
    {
        if (FiniteFloatValidator.IsFinite(value) && value >= min && value <= max)
            return value;
        Reject($"{name} {value} is outside {min} to {max}, using {fallback}");
        return fallback;
    }

    private void Reject(string message)
    {
        _rejected = true;
        _logger.LogWarning($"HeroAurasConfigProvider: {message}");
    }
}
