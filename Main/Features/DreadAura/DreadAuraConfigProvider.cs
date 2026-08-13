using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.DreadAura.Domain;

namespace TAOM.Features.DreadAura;

/// <summary>
/// Validating boundary loader for <c>dread_aura/dread_aura_config.json</c>
/// (BannerBearerConfigProvider pattern): missing file gives defaults + warn, a parse failure gives
/// defaults + error, and a parseable-but-invalid value reverts that one field to the compiled
/// default with a warning (csharp-architecture.md "Config Providers MUST Validate").
///
/// Not validated here, deliberately: race names and hero StringIds. Race names need the FaceGen
/// registry, which is not populated at config-load time, so <see cref="DreadRegistry"/> validates
/// them lazily on first resolve and skips + warns per entry. Hero ids are pinned instead by
/// ShippedDreadAuraConfigTests against the shipped lord data.
/// </summary>
public sealed class DreadAuraConfigProvider : IDreadAuraConfigProvider
{
    // Replace, not append-merge: Json.NET's default ObjectCreationHandling appends JSON collection
    // entries onto the constructor-populated defaults, so an author who lists one race would get
    // their entry PLUS every compiled one.
    private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
    {
        ObjectCreationHandling = ObjectCreationHandling.Replace,
    };

    // Above ~30 m a battlefield aura stops being a hazard you can walk around. This is a design
    // ceiling, not an engine one: Mission.GetNearbyEnemyAgents is pure native paging with no
    // proximity-map fallback, so there is nothing here to degrade (that hazard belongs to
    // AgentProximityMap.BeginSearch, which this feature does not use).
    private const float MaxRadius = 30f;

    // A pulse faster than 0.1s buys nothing (drain integrates over elapsed time either way) and
    // multiplies the static-locked proximity query. Slower than 5s and the aura visibly stutters.
    private const float MinPulseInterval = 0.1f;
    private const float MaxPulseInterval = 5f;

    private const float MaxMoralePerSecond = 50f;

    // The engine clamps initial morale to [15, 100], so a floor above 50 would disable the aura
    // for every troop in the game.
    private const float MaxMoraleFloor = 50f;

    private const int MaxSourcesPerFrameCeiling = 16;

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<DreadAuraConfig> _config;

    public DreadAuraConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _config = new Lazy<DreadAuraConfig>(LoadConfig);
    }

    public DreadAuraConfig GetConfig() => _config.Value;

    private DreadAuraConfig LoadConfig()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "dread_aura", "dread_aura_config.json");

        if (!File.Exists(path))
        {
            _logger.LogWarning($"DreadAuraConfigProvider: dread_aura_config.json not found at {path}, using defaults");
            return new DreadAuraConfig();
        }

        DreadAuraConfig parsed;
        try
        {
            var json = File.ReadAllText(path);
            parsed = JsonConvert.DeserializeObject<DreadAuraConfig>(json, SerializerSettings) ?? new DreadAuraConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError($"DreadAuraConfigProvider: Failed to parse dread_aura_config.json: {ex.Message}");
            return new DreadAuraConfig();
        }

        return Validate(parsed);
    }

    private DreadAuraConfig Validate(DreadAuraConfig parsed)
    {
        var defaults = new DreadAuraConfig();
        var rejected = false;

        var profile = ValidateProfile(parsed.Profile, defaults.Profile, ref rejected);

        var sanitized = new DreadAuraConfig
        {
            Enabled = parsed.Enabled,
            PulseIntervalSeconds = ValidateFloat(
                parsed.PulseIntervalSeconds, defaults.PulseIntervalSeconds,
                MinPulseInterval, MaxPulseInterval, nameof(parsed.PulseIntervalSeconds), ref rejected),
            MaxSourcesPerFrame = ValidateInt(
                parsed.MaxSourcesPerFrame, defaults.MaxSourcesPerFrame,
                1, MaxSourcesPerFrameCeiling, nameof(parsed.MaxSourcesPerFrame), ref rejected),
            FearVoiceChancePerPulse = ValidateFloat(
                parsed.FearVoiceChancePerPulse, defaults.FearVoiceChancePerPulse,
                0f, 1f, nameof(parsed.FearVoiceChancePerPulse), ref rejected),
            HeroSets = ValidateList(parsed.HeroSets, defaults.HeroSets, nameof(parsed.HeroSets), ref rejected),
            HeroIds = ValidateList(parsed.HeroIds, defaults.HeroIds, nameof(parsed.HeroIds), ref rejected),
            Races = ValidateList(parsed.Races, defaults.Races, nameof(parsed.Races), ref rejected),
            Profile = profile,
            RaceResist = ValidateResist(parsed.RaceResist, defaults.RaceResist, ref rejected),
        };

        if (rejected)
            _logger.LogWarning("DreadAuraConfigProvider: dread_aura_config.json contained invalid values. See prior warnings for details.");
        else
            _logger.LogInfo("DreadAuraConfigProvider: Loaded dread_aura_config.json");

        return sanitized;
    }

    private DreadProfileConfig ValidateProfile(DreadProfileConfig parsed, DreadProfileConfig defaults, ref bool rejected)
    {
        if (parsed == null)
        {
            _logger.LogWarning("DreadAuraConfigProvider: profile is null, reverting to defaults");
            rejected = true;
            return defaults;
        }

        var radius = ValidateFloat(parsed.Radius, defaults.Radius, 0.1f, MaxRadius, "profile.radius", ref rejected);

        // Ordering invariant, checked against the ALREADY-VALIDATED radius: an inner radius above
        // the outer one would invert the falloff band. The service degrades gracefully if it ever
        // sees that state at runtime (the MCM radius slider can drop below this value), but a
        // config that ships it is an authoring mistake worth a warning.
        var innerRadius = ValidateFloat(
            parsed.InnerRadius, Math.Min(defaults.InnerRadius, radius), 0f, radius, "profile.innerRadius", ref rejected);

        return new DreadProfileConfig
        {
            Radius = radius,
            InnerRadius = innerRadius,
            MoralePerSecond = ValidateFloat(
                parsed.MoralePerSecond, defaults.MoralePerSecond, 0f, MaxMoralePerSecond, "profile.moralePerSecond", ref rejected),
            MoraleFloor = ValidateFloat(
                parsed.MoraleFloor, defaults.MoraleFloor, 0f, MaxMoraleFloor, "profile.moraleFloor", ref rejected),
        };
    }

    // FiniteFloatValidator FIRST: a bare `value < min || value > max` is false for NaN, so a NaN
    // would sail through the range check and poison every downstream comparison.
    private float ValidateFloat(float value, float fallback, float min, float max, string field, ref bool rejected)
    {
        if (!FiniteFloatValidator.IsFiniteInRange(value, min, max))
        {
            _logger.LogWarning($"DreadAuraConfigProvider: {field} = {value} is not a finite value in [{min}, {max}], reverting to {fallback}");
            rejected = true;
            return fallback;
        }

        return value;
    }

    private int ValidateInt(int value, int fallback, int min, int max, string field, ref bool rejected)
    {
        if (value < min || value > max)
        {
            _logger.LogWarning($"DreadAuraConfigProvider: {field} = {value} is outside [{min}, {max}], reverting to {fallback}");
            rejected = true;
            return fallback;
        }

        return value;
    }

    // An EMPTY list is a legitimate "no sources of this kind" switch and passes through; only a
    // null (a JSON `null` or a missing-with-Replace-semantics key) reverts.
    private List<string> ValidateList(List<string> value, List<string> fallback, string field, ref bool rejected)
    {
        if (value == null)
        {
            _logger.LogWarning($"DreadAuraConfigProvider: {field} is null, reverting to defaults");
            rejected = true;
            return fallback;
        }

        return value;
    }

    private Dictionary<string, float> ValidateResist(
        Dictionary<string, float> value, Dictionary<string, float> fallback, ref bool rejected)
    {
        if (value == null)
        {
            _logger.LogWarning("DreadAuraConfigProvider: raceResist is null, reverting to defaults");
            rejected = true;
            return fallback;
        }

        var kept = new Dictionary<string, float>();
        foreach (var pair in value)
        {
            // Above 1.0 would AMPLIFY dread on a race the author is trying to protect — the sign
            // trap, in multiplier form. Drop the row rather than clamping it, so the author sees
            // the mistake instead of silently getting a different number.
            if (!FiniteFloatValidator.IsFiniteInRange(pair.Value, 0f, 1f))
            {
                _logger.LogWarning($"DreadAuraConfigProvider: raceResist['{pair.Key}'] = {pair.Value} is not a finite value in [0, 1], dropping the entry");
                rejected = true;
                continue;
            }

            kept[pair.Key] = pair.Value;
        }

        return kept;
    }
}
