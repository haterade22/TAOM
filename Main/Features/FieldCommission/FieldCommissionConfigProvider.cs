using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.FieldCommission.Domain;

namespace TAOM.Features.FieldCommission;

/// <summary>
/// Loads + validates <c>field_commission/field_commission_config.json</c>. Mirrors
/// <c>CultureConversionConfigProvider</c>: <see cref="Lazy{T}"/> singleton, semantic validation with
/// per-field revert-to-default + warning, NaN/Infinity guards on every float field. Reuse.Singleton —
/// changes require a full game restart, not a save-load.
/// </summary>
public class FieldCommissionConfigProvider : IFieldCommissionConfigProvider
{
    private static readonly string[] DefaultAllowedRaceNames = { "human", "dwarf", "elf" };

    // Replace, not append-merge: Json.NET's default ObjectCreationHandling appends JSON list
    // entries onto the constructor-populated default AllowedRaceNames list (a JSON
    // ["human"] would become ["human","dwarf","elf","human"]). Replace makes a JSON list
    // REPLACE the compiled default instead.
    private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
    {
        ObjectCreationHandling = ObjectCreationHandling.Replace,
    };

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<FieldCommissionConfig> _config;

    public FieldCommissionConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _config = new Lazy<FieldCommissionConfig>(LoadConfig);
    }

    public FieldCommissionConfig GetConfig() => _config.Value;

    private FieldCommissionConfig LoadConfig()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "field_commission", "field_commission_config.json");

        if (!File.Exists(path))
        {
            _logger.LogWarning($"FieldCommissionConfigProvider: field_commission_config.json not found at {path}, using defaults");
            return new FieldCommissionConfig();
        }

        FieldCommissionConfig parsed;
        try
        {
            var json = File.ReadAllText(path);
            parsed = JsonConvert.DeserializeObject<FieldCommissionConfig>(json, SerializerSettings) ?? new FieldCommissionConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError($"FieldCommissionConfigProvider: Failed to parse field_commission_config.json: {ex.Message}");
            return new FieldCommissionConfig();
        }

        return Validate(parsed);
    }

    private FieldCommissionConfig Validate(FieldCommissionConfig parsed)
    {
        var defaults = new FieldCommissionConfig();
        var sanitized = new FieldCommissionConfig
        {
            Enabled = parsed.Enabled,
            RatioThreshold = parsed.RatioThreshold,
            MeritPerKill = parsed.MeritPerKill,
            MeritThreshold = parsed.MeritThreshold,
            RetainerAllowance = parsed.RetainerAllowance,
            MaxOffersPerBattle = parsed.MaxOffersPerBattle,
            Diagnostics = parsed.Diagnostics,
            SkillPointsPerLevel = parsed.SkillPointsPerLevel,
            AllowedRaceNames = parsed.AllowedRaceNames,
        };

        var rejected = false;

        // 0 is a legitimate (if extreme — "never eligible") choice; only negative/NaN/Infinity revert.
        if (!FiniteFloatValidator.IsFiniteAtLeast(sanitized.RatioThreshold, 0f))
        {
            _logger.LogWarning($"FieldCommissionConfigProvider: ratioThreshold={sanitized.RatioThreshold} must be finite and >= 0, reverting to default {defaults.RatioThreshold}");
            sanitized.RatioThreshold = defaults.RatioThreshold;
            rejected = true;
        }

        if (sanitized.MeritPerKill < 1)
        {
            _logger.LogWarning($"FieldCommissionConfigProvider: meritPerKill={sanitized.MeritPerKill} must be >= 1, reverting to default {defaults.MeritPerKill}");
            sanitized.MeritPerKill = defaults.MeritPerKill;
            rejected = true;
        }

        if (sanitized.MeritThreshold < 1)
        {
            _logger.LogWarning($"FieldCommissionConfigProvider: meritThreshold={sanitized.MeritThreshold} must be >= 1, reverting to default {defaults.MeritThreshold}");
            sanitized.MeritThreshold = defaults.MeritThreshold;
            rejected = true;
        }

        if (sanitized.SkillPointsPerLevel < 1)
        {
            _logger.LogWarning($"FieldCommissionConfigProvider: skillPointsPerLevel={sanitized.SkillPointsPerLevel} must be >= 1, reverting to default {defaults.SkillPointsPerLevel}");
            sanitized.SkillPointsPerLevel = defaults.SkillPointsPerLevel;
            rejected = true;
        }

        if (sanitized.RetainerAllowance < 0)
        {
            _logger.LogWarning($"FieldCommissionConfigProvider: retainerAllowance={sanitized.RetainerAllowance} must be >= 0, reverting to default {defaults.RetainerAllowance}");
            sanitized.RetainerAllowance = defaults.RetainerAllowance;
            rejected = true;
        }

        // 0 would read as "promotions are on" while silently queueing nothing — a setting that lies
        // about what it does. The master toggle is the honest way to turn the feature off.
        if (sanitized.MaxOffersPerBattle < 1)
        {
            _logger.LogWarning($"FieldCommissionConfigProvider: maxOffersPerBattle={sanitized.MaxOffersPerBattle} must be >= 1, reverting to default {defaults.MaxOffersPerBattle}");
            sanitized.MaxOffersPerBattle = defaults.MaxOffersPerBattle;
            rejected = true;
        }

        sanitized.AllowedRaceNames = SanitizeRaceNames(sanitized.AllowedRaceNames);

        if (rejected)
            _logger.LogWarning("FieldCommissionConfigProvider: field_commission_config.json contained invalid values. See prior warnings for details.");
        else
            _logger.LogInfo("FieldCommissionConfigProvider: Loaded field_commission_config.json");

        return sanitized;
    }

    private static List<string> SanitizeRaceNames(List<string> raceNames)
    {
        if (raceNames == null)
            return new List<string>(DefaultAllowedRaceNames);

        // Drop null/blank entries — a typo'd empty string must not silently become "no race is
        // allowed" or (worse) match an unvalidated lookup's fallback name (validate-before-lookup rule).
        return raceNames.Where(name => !string.IsNullOrWhiteSpace(name)).ToList();
    }
}
