using System;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;

namespace TAOM.Features.CultureConversion;

/// <summary>
/// Loads + validates <c>culture_conversion/culture_conversion_config.json</c>. Mirrors
/// <c>RevoltTuningConfigProvider</c>: <see cref="Lazy{T}"/> singleton, semantic validation with
/// per-field revert-to-default + warning, NaN/Infinity guards on the float field. Reuse.Singleton —
/// changes require a full game restart, not a save-load.
/// </summary>
public class CultureConversionConfigProvider : ICultureConversionConfigProvider
{
    private const int MinHoldDays = 1;
    private const int MaxHoldDays = 100000; // ~273 in-game years — effectively "never" without overflow risk

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<CultureConversionConfig> _config;

    public CultureConversionConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _config = new Lazy<CultureConversionConfig>(LoadConfig);
    }

    public CultureConversionConfig GetConfig() => _config.Value;

    private CultureConversionConfig LoadConfig()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "culture_conversion", "culture_conversion_config.json");

        if (!File.Exists(path))
        {
            _logger.LogWarning($"CultureConversionConfigProvider: culture_conversion_config.json not found at {path}, using defaults");
            return new CultureConversionConfig();
        }

        CultureConversionConfig parsed;
        try
        {
            var json = File.ReadAllText(path);
            parsed = JsonConvert.DeserializeObject<CultureConversionConfig>(json) ?? new CultureConversionConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError($"CultureConversionConfigProvider: Failed to parse culture_conversion_config.json: {ex.Message}");
            return new CultureConversionConfig();
        }

        return Validate(parsed);
    }

    private CultureConversionConfig Validate(CultureConversionConfig parsed)
    {
        var sanitized = new CultureConversionConfig
        {
            Enabled = parsed.Enabled,
            RequiredHoldDays = parsed.RequiredHoldDays,
            RequireStableLoyalty = parsed.RequireStableLoyalty,
            MinLoyaltyToConvert = parsed.MinLoyaltyToConvert,
            ConvertPlayerOwnedSettlements = parsed.ConvertPlayerOwnedSettlements,
            ReplaceNotablesOnConversion = parsed.ReplaceNotablesOnConversion,
        };

        var defaults = new CultureConversionConfig();
        var rejected = false;

        if (sanitized.RequiredHoldDays < MinHoldDays || sanitized.RequiredHoldDays > MaxHoldDays)
        {
            _logger.LogWarning($"CultureConversionConfigProvider: requiredHoldDays={sanitized.RequiredHoldDays} outside [{MinHoldDays},{MaxHoldDays}], reverting to default {defaults.RequiredHoldDays}");
            sanitized.RequiredHoldDays = defaults.RequiredHoldDays;
            rejected = true;
        }

        if (!FiniteFloatValidator.IsFiniteInRange(sanitized.MinLoyaltyToConvert, 0f, 100f))
        {
            _logger.LogWarning($"CultureConversionConfigProvider: minLoyaltyToConvert={sanitized.MinLoyaltyToConvert} must be a finite value in [0,100], reverting to default {defaults.MinLoyaltyToConvert}");
            sanitized.MinLoyaltyToConvert = defaults.MinLoyaltyToConvert;
            rejected = true;
        }

        if (rejected)
            _logger.LogWarning("CultureConversionConfigProvider: culture_conversion_config.json contained invalid values. See prior warnings for details.");
        else
            _logger.LogInfo("CultureConversionConfigProvider: Loaded culture_conversion_config.json");

        return sanitized;
    }
}
