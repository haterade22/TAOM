using System;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;

namespace TAOM.Features.WarChronicle.Rally;

/// <summary>
/// Loads and validates <c>war_chronicle/rally.json</c>. Same shape as MomentumConfigProvider: a
/// <see cref="Lazy{T}"/> singleton, every float finite-checked before its range check, a bad field
/// reverts to the compiled default with a warning, and one summary warning follows any revert.
/// The ordering rules (exit below enter in a tier, tier 1 not above tier 2) cannot be repaired field by
/// field, so a broken ordering reverts its whole group: the four loss thresholds, or the four magnitudes.
/// Reuse.Singleton, so an edit needs a full game restart, not a save-load. The MCM <c>WarRallyEnabled</c>
/// switch is separate and takes effect at the next daily tick.
/// </summary>
public class RallyConfigProvider : IRallyConfigProvider
{
    internal const float MinTtlHours = 24f;
    internal const float MaxTtlHours = 168f;
    internal const float MaxMagnitude = 2f;

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<RallyConfig> _config;
    private bool _rejected;

    public RallyConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _config = new Lazy<RallyConfig>(LoadConfig);
    }

    public RallyConfig GetConfig() => _config.Value;

    private RallyConfig LoadConfig()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "war_chronicle", "rally.json");
        if (!File.Exists(path))
        {
            _logger.LogWarning($"RallyConfigProvider: rally.json not found at {path}, using the compiled defaults");
            return new RallyConfig();
        }

        RallyConfig parsed;
        try
        {
            parsed = JsonConvert.DeserializeObject<RallyConfig>(File.ReadAllText(path)) ?? new RallyConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError($"RallyConfigProvider: Failed to parse rally.json: {ex.Message}");
            return new RallyConfig();
        }

        return Validate(parsed);
    }

    private RallyConfig Validate(RallyConfig parsed)
    {
        var defaults = new RallyConfig();
        _rejected = false;

        // Newtonsoft leaves a nested object null when the JSON says null.
        parsed.Tier1 = parsed.Tier1 ?? defaults.Tier1;
        parsed.Tier2 = parsed.Tier2 ?? defaults.Tier2;

        parsed.EffectTtlHours = Checked("effectTtlHours", parsed.EffectTtlHours, defaults.EffectTtlHours,
            FiniteFloatValidator.IsFiniteInRange(parsed.EffectTtlHours, MinTtlHours, MaxTtlHours),
            $"must be finite and within {MinTtlHours}..{MaxTtlHours}");

        ValidateTier("tier1", parsed.Tier1, defaults.Tier1);
        ValidateTier("tier2", parsed.Tier2, defaults.Tier2);
        ValidateThresholdOrdering(parsed, defaults);
        ValidateMagnitudeOrdering(parsed, defaults);

        if (_rejected)
            _logger.LogWarning("RallyConfigProvider: rally.json contained invalid values. See prior warnings for details.");
        else
            _logger.LogInfo("RallyConfigProvider: Loaded rally.json");

        return parsed;
    }

    private void ValidateTier(string name, RallyTierConfig tier, RallyTierConfig defaults)
    {
        tier.EnterLoss = Loss(name + ".enterLoss", tier.EnterLoss, defaults.EnterLoss);
        tier.ExitLoss = Loss(name + ".exitLoss", tier.ExitLoss, defaults.ExitLoss);
        tier.VolunteerRate = Magnitude(name + ".volunteerRate", tier.VolunteerRate, defaults.VolunteerRate);
        tier.PrisonerEscape = Magnitude(name + ".prisonerEscape", tier.PrisonerEscape, defaults.PrisonerEscape);
    }

    private void ValidateThresholdOrdering(RallyConfig parsed, RallyConfig defaults)
    {
        var broken = false;
        broken |= Violates(parsed.Tier1.ExitLoss < parsed.Tier1.EnterLoss,
            $"tier1.exitLoss={Inv(parsed.Tier1.ExitLoss)} must be below tier1.enterLoss={Inv(parsed.Tier1.EnterLoss)}");
        broken |= Violates(parsed.Tier2.ExitLoss < parsed.Tier2.EnterLoss,
            $"tier2.exitLoss={Inv(parsed.Tier2.ExitLoss)} must be below tier2.enterLoss={Inv(parsed.Tier2.EnterLoss)}");
        broken |= Violates(parsed.Tier1.EnterLoss <= parsed.Tier2.EnterLoss,
            $"tier1.enterLoss={Inv(parsed.Tier1.EnterLoss)} must not exceed tier2.enterLoss={Inv(parsed.Tier2.EnterLoss)}");
        broken |= Violates(parsed.Tier1.ExitLoss <= parsed.Tier2.ExitLoss,
            $"tier1.exitLoss={Inv(parsed.Tier1.ExitLoss)} must not exceed tier2.exitLoss={Inv(parsed.Tier2.ExitLoss)}");

        if (!broken)
            return;

        parsed.Tier1.EnterLoss = defaults.Tier1.EnterLoss;
        parsed.Tier1.ExitLoss = defaults.Tier1.ExitLoss;
        parsed.Tier2.EnterLoss = defaults.Tier2.EnterLoss;
        parsed.Tier2.ExitLoss = defaults.Tier2.ExitLoss;
    }

    private void ValidateMagnitudeOrdering(RallyConfig parsed, RallyConfig defaults)
    {
        var broken = false;
        broken |= Violates(parsed.Tier2.VolunteerRate >= parsed.Tier1.VolunteerRate,
            $"tier2.volunteerRate={Inv(parsed.Tier2.VolunteerRate)} must not be below tier1.volunteerRate={Inv(parsed.Tier1.VolunteerRate)}");
        broken |= Violates(parsed.Tier2.PrisonerEscape >= parsed.Tier1.PrisonerEscape,
            $"tier2.prisonerEscape={Inv(parsed.Tier2.PrisonerEscape)} must not be below tier1.prisonerEscape={Inv(parsed.Tier1.PrisonerEscape)}");

        if (!broken)
            return;

        parsed.Tier1.VolunteerRate = defaults.Tier1.VolunteerRate;
        parsed.Tier1.PrisonerEscape = defaults.Tier1.PrisonerEscape;
        parsed.Tier2.VolunteerRate = defaults.Tier2.VolunteerRate;
        parsed.Tier2.PrisonerEscape = defaults.Tier2.PrisonerEscape;
    }

    // A loss is a fraction of the baseline: strictly between 0 and 1.
    private float Loss(string name, float value, float fallback) =>
        Checked(name, value, fallback,
            FiniteFloatValidator.IsFinite(value) && value > 0f && value < 1f,
            "must be finite and between 0 and 1 (exclusive)");

    private float Magnitude(string name, float value, float fallback) =>
        Checked(name, value, fallback,
            FiniteFloatValidator.IsFiniteInRange(value, 0f, MaxMagnitude),
            $"must be finite and within 0..{MaxMagnitude}");

    private float Checked(string name, float value, float fallback, bool valid, string rule)
    {
        if (valid)
            return value;

        _rejected = true;
        _logger.LogWarning($"RallyConfigProvider: {name}={Inv(value)} {rule}, reverting to default {Inv(fallback)}");
        return fallback;
    }

    // The ordering tests are written as the holding condition, so a NaN (already reverted above) could
    // not slip through a negated comparison.
    private bool Violates(bool holds, string message)
    {
        if (holds)
            return false;

        _rejected = true;
        _logger.LogWarning($"RallyConfigProvider: {message}, reverting the group to its defaults");
        return true;
    }

    private static string Inv(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
