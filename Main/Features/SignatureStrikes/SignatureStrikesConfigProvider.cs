using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.SignatureStrikes.Domain;

namespace TAOM.Features.SignatureStrikes;

/// <summary>
/// Validating boundary loader for <c>signature_strikes/signature_strikes_config.json</c>
/// (DreadAuraConfigProvider pattern): missing file gives defaults + warn, a parse failure gives
/// defaults + error, and a parseable-but-invalid value reverts that one field to the compiled
/// default with a warning (csharp-architecture.md "Config Providers MUST Validate"). A strike row
/// whose direction or kind name is unknown is dropped with a warning rather than kept: the
/// service could never match it, and a row the author believes is live must not be silent.
///
/// Not validated here, deliberately: race names and hero StringIds. Race names need the FaceGen
/// registry, which is not populated at config-load time, so <see cref="SignatureStrikeRegistry"/>
/// validates them lazily; hero ids are pinned by ShippedSignatureStrikesConfigTests.
/// </summary>
public sealed class SignatureStrikesConfigProvider : ISignatureStrikesConfigProvider
{
    private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
    {
        ObjectCreationHandling = ObjectCreationHandling.Replace,
    };

    // A 15 m ring is already five boulder radii; past that a "melee" strike is an artillery
    // strike. Cooldowns above two minutes make a strike a once-per-battle event.
    private const float MinRadius = 0.1f;
    private const float MaxRadius = 15f;
    // A swing's later cleave bodies land within a fraction of a second of the first; the floor is
    // what keeps "one package per swing" true for every config the provider accepts (Codex 114, O3).
    private const float MinCooldownSeconds = 0.5f;
    private const float MaxCooldownSeconds = 120f;
    private const float MaxMagnitude = 500f;
    private const float MaxFearMorale = 100f;
    private const int MaxWorldHitBaseDamage = 500;

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<SignatureStrikesConfig> _config;

    public SignatureStrikesConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _config = new Lazy<SignatureStrikesConfig>(LoadConfig);
    }

    public SignatureStrikesConfig GetConfig() => _config.Value;

    private SignatureStrikesConfig LoadConfig()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "signature_strikes", "signature_strikes_config.json");

        if (!File.Exists(path))
        {
            _logger.LogWarning($"SignatureStrikesConfigProvider: signature_strikes_config.json not found at {path}, using defaults");
            return new SignatureStrikesConfig();
        }

        SignatureStrikesConfig parsed;
        try
        {
            var json = File.ReadAllText(path);
            parsed = JsonConvert.DeserializeObject<SignatureStrikesConfig>(json, SerializerSettings) ?? new SignatureStrikesConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError($"SignatureStrikesConfigProvider: Failed to parse signature_strikes_config.json: {ex.Message}");
            return new SignatureStrikesConfig();
        }

        return Validate(parsed);
    }

    private SignatureStrikesConfig Validate(SignatureStrikesConfig parsed)
    {
        var defaults = new SignatureStrikesConfig();
        var rejected = false;

        var sanitized = new SignatureStrikesConfig
        {
            Enabled = parsed.Enabled,
            HeroIds = ValidateList(parsed.HeroIds, defaults.HeroIds, "heroIds", ref rejected),
            Races = ValidateList(parsed.Races, defaults.Races, "races", ref rejected),
            SlamCooldownSeconds = ValidateFloat(
                parsed.SlamCooldownSeconds, defaults.SlamCooldownSeconds,
                MinCooldownSeconds, MaxCooldownSeconds, "slamCooldownSeconds", ref rejected),
            SweepCooldownSeconds = ValidateFloat(
                parsed.SweepCooldownSeconds, defaults.SweepCooldownSeconds,
                MinCooldownSeconds, MaxCooldownSeconds, "sweepCooldownSeconds", ref rejected),
            ShieldBlockedMultiplier = ValidateFloat(
                parsed.ShieldBlockedMultiplier, defaults.ShieldBlockedMultiplier,
                0f, 1f, "shieldBlockedMultiplier", ref rejected),
            Strikes = ValidateStrikes(parsed.Strikes, defaults.Strikes, ref rejected),
        };

        if (rejected)
            _logger.LogWarning("SignatureStrikesConfigProvider: signature_strikes_config.json contained invalid values. See prior warnings for details.");
        else
            _logger.LogInfo("SignatureStrikesConfigProvider: Loaded signature_strikes_config.json");

        return sanitized;
    }

    private Dictionary<string, StrikeProfileConfig> ValidateStrikes(
        Dictionary<string, StrikeProfileConfig> parsed,
        Dictionary<string, StrikeProfileConfig> defaults,
        ref bool rejected)
    {
        if (parsed == null)
        {
            _logger.LogWarning("SignatureStrikesConfigProvider: strikes is null, reverting to defaults");
            rejected = true;
            return defaults;
        }

        // An EMPTY dictionary is a legitimate "no strikes" switch and passes through.
        var kept = new Dictionary<string, StrikeProfileConfig>();
        foreach (var pair in parsed)
        {
            if (!StrikeNames.TryParseDirection(pair.Key, out var direction))
            {
                _logger.LogWarning($"SignatureStrikesConfigProvider: strikes['{pair.Key}'] is not a swing direction (Overhead, Left, Right, Thrust), dropping the row");
                rejected = true;
                continue;
            }

            var key = direction.ToString();
            if (pair.Value == null)
            {
                _logger.LogWarning($"SignatureStrikesConfigProvider: strikes['{key}'] is null, dropping the row");
                rejected = true;
                continue;
            }

            if (!StrikeNames.TryParseKind(pair.Value.Kind, out _))
            {
                _logger.LogWarning($"SignatureStrikesConfigProvider: strikes['{key}'].kind = '{pair.Value.Kind}' is not a strike kind (Slam, Sweep), dropping the row");
                rejected = true;
                continue;
            }

            // Revert to THIS direction's compiled default where one exists, else the neutral one.
            var fallback = defaults.TryGetValue(key, out var directionDefault) ? directionDefault : new StrikeProfileConfig();
            kept[key] = ValidateProfile(pair.Value, fallback, key, ref rejected);
        }

        return kept;
    }

    private StrikeProfileConfig ValidateProfile(StrikeProfileConfig parsed, StrikeProfileConfig fallback, string key, ref bool rejected)
    {
        var outer = ValidateFloat(parsed.OuterRadius, fallback.OuterRadius, MinRadius, MaxRadius, $"strikes['{key}'].outerRadius", ref rejected);

        // Ordering invariant, checked against the ALREADY-VALIDATED outer radius: an inner radius
        // above the outer one would invert the falloff band.
        var inner = ValidateFloat(
            parsed.InnerRadius, Math.Min(fallback.InnerRadius, outer), 0f, outer, $"strikes['{key}'].innerRadius", ref rejected);

        return new StrikeProfileConfig
        {
            Kind = parsed.Kind,
            OuterRadius = outer,
            InnerRadius = inner,
            DamageFraction = ValidateFloat(
                parsed.DamageFraction, fallback.DamageFraction, 0f, 1f, $"strikes['{key}'].damageFraction", ref rejected),
            WorldHitBaseDamage = ValidateInt(
                parsed.WorldHitBaseDamage, fallback.WorldHitBaseDamage, 0, MaxWorldHitBaseDamage, $"strikes['{key}'].worldHitBaseDamage", ref rejected),
            Magnitude = ValidateFloat(
                parsed.Magnitude, fallback.Magnitude, 0f, MaxMagnitude, $"strikes['{key}'].magnitude", ref rejected),
            KnockDown = parsed.KnockDown,
            KnockBack = parsed.KnockBack,
            FearMorale = ValidateFloat(
                parsed.FearMorale, fallback.FearMorale, 0f, MaxFearMorale, $"strikes['{key}'].fearMorale", ref rejected),
        };
    }

    // FiniteFloatValidator FIRST: a bare `value < min || value > max` is false for NaN, so a NaN
    // would sail through the range check and poison every downstream comparison.
    private float ValidateFloat(float value, float fallback, float min, float max, string field, ref bool rejected)
    {
        if (!FiniteFloatValidator.IsFiniteInRange(value, min, max))
        {
            _logger.LogWarning($"SignatureStrikesConfigProvider: {field} = {value} is not a finite value in [{min}, {max}], reverting to {fallback}");
            rejected = true;
            return fallback;
        }

        return value;
    }

    private int ValidateInt(int value, int fallback, int min, int max, string field, ref bool rejected)
    {
        if (value < min || value > max)
        {
            _logger.LogWarning($"SignatureStrikesConfigProvider: {field} = {value} is outside [{min}, {max}], reverting to {fallback}");
            rejected = true;
            return fallback;
        }

        return value;
    }

    // An EMPTY list is a legitimate "nobody on this axis" switch and passes through; only a null
    // (a JSON `null`) reverts.
    private List<string> ValidateList(List<string> value, List<string> fallback, string field, ref bool rejected)
    {
        if (value == null)
        {
            _logger.LogWarning($"SignatureStrikesConfigProvider: {field} is null, reverting to defaults");
            rejected = true;
            return fallback;
        }

        return value;
    }
}
