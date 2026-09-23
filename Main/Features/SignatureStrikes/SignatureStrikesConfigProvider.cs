using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
/// default of the signature with the same id, or to the neutral default for an id the compiled
/// set does not know, with a warning (csharp-architecture.md "Config Providers MUST Validate").
///
/// What is dropped rather than reverted, each with a warning, because keeping it would be silent:
/// a strike row whose direction or kind name is unknown (the service could never match it), a
/// strike whose kind has no cooldown entry (it would fire on every swing), a signature with no id
/// or a repeated one, and a signature that names nobody.
///
/// Not validated here, deliberately: race names, hero set names and hero StringIds. Race names
/// need the FaceGen registry, which is not populated at config-load time, so
/// <see cref="SignatureStrikeRegistry"/> validates them and the hero sets lazily; hero ids are
/// pinned by ShippedSignatureStrikesConfigTests.
/// </summary>
public sealed class SignatureStrikesConfigProvider : ISignatureStrikesConfigProvider
{
    private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
    {
        ObjectCreationHandling = ObjectCreationHandling.Replace,
    };

    // The shape of every module_sounds.xml name and of an FMOD "event:/..." path.
    private static readonly Regex SoundNamePattern = new Regex(@"^[A-Za-z0-9_:/.\-]+$", RegexOptions.CultureInvariant);

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
            ShieldBlockedMultiplier = ValidateFloat(
                parsed.ShieldBlockedMultiplier, defaults.ShieldBlockedMultiplier,
                0f, 1f, "shieldBlockedMultiplier", ref rejected),
            Signatures = ValidateSignatures(parsed.Signatures, defaults.Signatures, ref rejected),
        };

        if (rejected)
            _logger.LogWarning("SignatureStrikesConfigProvider: signature_strikes_config.json contained invalid values. See prior warnings for details.");
        else
            _logger.LogInfo("SignatureStrikesConfigProvider: Loaded signature_strikes_config.json");

        return sanitized;
    }

    private List<SignatureConfig> ValidateSignatures(List<SignatureConfig>? parsed, List<SignatureConfig> defaults, ref bool rejected)
    {
        if (parsed == null)
        {
            _logger.LogWarning("SignatureStrikesConfigProvider: signatures is null, reverting to defaults");
            rejected = true;
            return defaults;
        }

        // An EMPTY list is a legitimate "nobody has signature strikes" switch and passes through.
        var kept = new List<SignatureConfig>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < parsed.Count; i++)
        {
            var signature = parsed[i];
            if (signature == null)
            {
                _logger.LogWarning($"SignatureStrikesConfigProvider: signatures[{i}] is null, dropping it");
                rejected = true;
                continue;
            }

            var id = signature.Id?.Trim();
            if (string.IsNullOrEmpty(id))
            {
                _logger.LogWarning($"SignatureStrikesConfigProvider: signatures[{i}] has no id, dropping it");
                rejected = true;
                continue;
            }

            if (!seen.Add(id!))
            {
                _logger.LogWarning($"SignatureStrikesConfigProvider: signatures[{i}] repeats the id '{id}', dropping it (the first one wins)");
                rejected = true;
                continue;
            }

            var fallback = defaults.FirstOrDefault(d => string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase))
                ?? new SignatureConfig { Id = id! };
            var validated = ValidateSignature(signature, id!, fallback, ref rejected);
            if (validated != null)
                kept.Add(validated);
        }

        return kept;
    }

    private SignatureConfig? ValidateSignature(SignatureConfig parsed, string id, SignatureConfig fallback, ref bool rejected)
    {
        var label = $"signatures['{id}']";
        var heroIds = ValidateList(parsed.HeroIds, fallback.HeroIds, $"{label}.heroIds", ref rejected);
        var heroSets = ValidateList(parsed.HeroSets, fallback.HeroSets, $"{label}.heroSets", ref rejected);
        var races = ValidateList(parsed.Races, fallback.Races, $"{label}.races", ref rejected);

        if (heroIds.Count == 0 && heroSets.Count == 0 && races.Count == 0)
        {
            _logger.LogWarning($"SignatureStrikesConfigProvider: {label} names no hero id, hero set or race, so it matches nobody; dropping it");
            rejected = true;
            return null;
        }

        var cooldowns = ValidateCooldowns(parsed.Cooldowns, fallback.Cooldowns, label, ref rejected);
        return new SignatureConfig
        {
            Id = id,
            HeroIds = heroIds,
            HeroSets = heroSets,
            Races = races,
            Cooldowns = cooldowns,
            Strikes = ValidateStrikes(parsed.Strikes, fallback.Strikes, cooldowns, label, ref rejected),
        };
    }

    private Dictionary<string, float> ValidateCooldowns(
        Dictionary<string, float>? parsed, Dictionary<string, float> fallback, string label, ref bool rejected)
    {
        if (parsed == null)
        {
            _logger.LogWarning($"SignatureStrikesConfigProvider: {label}.cooldowns is null, reverting to defaults");
            rejected = true;
            parsed = fallback;
        }

        var kept = new Dictionary<string, float>();
        foreach (var pair in parsed)
        {
            if (!StrikeNames.TryParseKind(pair.Key, out var kind))
            {
                _logger.LogWarning($"SignatureStrikesConfigProvider: {label}.cooldowns['{pair.Key}'] is not a strike kind (Slam, Sweep, Scream), dropping it");
                rejected = true;
                continue;
            }

            var key = kind.ToString();
            if (FiniteFloatValidator.IsFiniteInRange(pair.Value, MinCooldownSeconds, MaxCooldownSeconds))
            {
                kept[key] = pair.Value;
                continue;
            }

            rejected = true;
            if (fallback.TryGetValue(key, out var compiled))
            {
                _logger.LogWarning($"SignatureStrikesConfigProvider: {label}.cooldowns['{key}'] = {pair.Value} is not a finite value in [{MinCooldownSeconds}, {MaxCooldownSeconds}], reverting to {compiled}");
                kept[key] = compiled;
            }
            else
            {
                _logger.LogWarning($"SignatureStrikesConfigProvider: {label}.cooldowns['{key}'] = {pair.Value} is not a finite value in [{MinCooldownSeconds}, {MaxCooldownSeconds}] and has no default, dropping it");
            }
        }

        return kept;
    }

    private Dictionary<string, StrikeProfileConfig> ValidateStrikes(
        Dictionary<string, StrikeProfileConfig>? parsed,
        Dictionary<string, StrikeProfileConfig> fallback,
        Dictionary<string, float> cooldowns,
        string label,
        ref bool rejected)
    {
        if (parsed == null)
        {
            _logger.LogWarning($"SignatureStrikesConfigProvider: {label}.strikes is null, reverting to defaults");
            rejected = true;
            parsed = fallback;
        }

        // An EMPTY dictionary is a legitimate "no strikes" switch and passes through.
        var kept = new Dictionary<string, StrikeProfileConfig>();
        foreach (var pair in parsed)
        {
            if (!StrikeNames.TryParseDirection(pair.Key, out var direction))
            {
                _logger.LogWarning($"SignatureStrikesConfigProvider: {label}.strikes['{pair.Key}'] is not a swing direction (Overhead, Left, Right, Thrust), dropping the row");
                rejected = true;
                continue;
            }

            var key = direction.ToString();
            var field = $"{label}.strikes['{key}']";
            if (pair.Value == null)
            {
                _logger.LogWarning($"SignatureStrikesConfigProvider: {field} is null, dropping the row");
                rejected = true;
                continue;
            }

            if (!StrikeNames.TryParseKind(pair.Value.Kind, out var kind))
            {
                _logger.LogWarning($"SignatureStrikesConfigProvider: {field}.kind = '{pair.Value.Kind}' is not a strike kind (Slam, Sweep, Scream), dropping the row");
                rejected = true;
                continue;
            }

            if (!cooldowns.ContainsKey(kind.ToString()))
            {
                _logger.LogWarning($"SignatureStrikesConfigProvider: {field}.kind {kind} has no cooldowns entry, dropping the row");
                rejected = true;
                continue;
            }

            // Revert to THIS direction's compiled default where one exists, else the neutral one.
            var rowFallback = fallback.TryGetValue(key, out var directionDefault) ? directionDefault : new StrikeProfileConfig();
            kept[key] = ValidateProfile(pair.Value, kind, rowFallback, field, ref rejected);
        }

        return kept;
    }

    private StrikeProfileConfig ValidateProfile(StrikeProfileConfig parsed, StrikeKind kind, StrikeProfileConfig fallback, string field, ref bool rejected)
    {
        var outer = ValidateFloat(parsed.OuterRadius, fallback.OuterRadius, MinRadius, MaxRadius, $"{field}.outerRadius", ref rejected);

        // Ordering invariant, checked against the ALREADY-VALIDATED outer radius: an inner radius
        // above the outer one would invert the falloff band.
        var inner = ValidateFloat(
            parsed.InnerRadius, Math.Min(fallback.InnerRadius, outer), 0f, outer, $"{field}.innerRadius", ref rejected);

        return new StrikeProfileConfig
        {
            Kind = kind.ToString(),
            Origin = ValidateOrigin(parsed.Origin, fallback.Origin, field, ref rejected),
            OuterRadius = outer,
            InnerRadius = inner,
            DamageFraction = ValidateFloat(
                parsed.DamageFraction, fallback.DamageFraction, 0f, 1f, $"{field}.damageFraction", ref rejected),
            WorldHitBaseDamage = ValidateInt(
                parsed.WorldHitBaseDamage, fallback.WorldHitBaseDamage, 0, MaxWorldHitBaseDamage, $"{field}.worldHitBaseDamage", ref rejected),
            Magnitude = ValidateFloat(
                parsed.Magnitude, fallback.Magnitude, 0f, MaxMagnitude, $"{field}.magnitude", ref rejected),
            KnockDown = parsed.KnockDown,
            KnockBack = parsed.KnockBack,
            FearMorale = ValidateFloat(
                parsed.FearMorale, fallback.FearMorale, 0f, MaxFearMorale, $"{field}.fearMorale", ref rejected),
            Sound = ValidateSound(parsed.Sound, fallback.Sound, field, ref rejected),
        };
    }

    // The origin decides where the ring lands, so an unknown name must not quietly become Impact
    // (the M1 trap): revert to this row's compiled origin and say so.
    private string ValidateOrigin(string? value, string fallback, string field, ref bool rejected)
    {
        if (StrikeNames.TryParseOrigin(value, out var origin))
            return origin.ToString();

        _logger.LogWarning($"SignatureStrikesConfigProvider: {field}.origin = '{value}' is not a strike origin (Impact, Self), reverting to {fallback}");
        rejected = true;
        return fallback;
    }

    // Blank is a legitimate "no sound". A name that cannot be a module sound reverts; a well-formed
    // name nothing registers is caught at runtime (the runner yells instead and warns once).
    private string? ValidateSound(string? value, string? fallback, string field, ref bool rejected)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value!.Trim();
        if (SoundNamePattern.IsMatch(trimmed))
            return trimmed;

        _logger.LogWarning($"SignatureStrikesConfigProvider: {field}.sound = '{value}' is not a module sound name (letters, digits, _ : / . -), reverting to {fallback ?? "none"}");
        rejected = true;
        return fallback;
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
    private List<string> ValidateList(List<string>? value, List<string> fallback, string field, ref bool rejected)
    {
        if (value == null)
        {
            _logger.LogWarning($"SignatureStrikesConfigProvider: {field} is null, reverting to defaults");
            rejected = true;
            return new List<string>(fallback);
        }

        return value;
    }
}
