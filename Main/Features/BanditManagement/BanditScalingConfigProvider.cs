using System;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;

namespace TAOM.Features.BanditManagement;

public class BanditScalingConfigProvider : IBanditScalingConfigProvider
{
    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<BanditScalingConfig> _config;

    public BanditScalingConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _config = new Lazy<BanditScalingConfig>(LoadConfig);
    }

    public BanditScalingConfig GetConfig() => _config.Value;

    private BanditScalingConfig LoadConfig()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "bandit_management", "bandit_scaling_config.json");

        if (!File.Exists(path))
        {
            _logger.LogWarning($"BanditScalingConfigProvider: bandit_scaling_config.json not found at {path}, using defaults");
            return new BanditScalingConfig();
        }

        BanditScalingConfig parsed;
        try
        {
            var json = File.ReadAllText(path);
            parsed = JsonConvert.DeserializeObject<BanditScalingConfig>(json) ?? new BanditScalingConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError($"BanditScalingConfigProvider: Failed to parse bandit_scaling_config.json: {ex.Message}");
            return new BanditScalingConfig();
        }

        return Validate(parsed);
    }

    private BanditScalingConfig Validate(BanditScalingConfig parsed)
    {
        var sanitized = new BanditScalingConfig
        {
            DensityCurve = parsed.DensityCurve,
            PartySizeCurve = parsed.PartySizeCurve,
            BossFightCurve = parsed.BossFightCurve,
            MaxHideoutsPerFactionCap = parsed.MaxHideoutsPerFactionCap,
            MaxPartiesPerHideoutCap = parsed.MaxPartiesPerHideoutCap,
            MinPartiesToInfest = parsed.MinPartiesToInfest,
        };

        var defaults = new BanditScalingConfig();
        var rejected = false;

        if (!FiniteFloatValidator.IsFiniteInRange(sanitized.DensityCurve, 0f, 5f))
        {
            _logger.LogWarning($"BanditScalingConfigProvider: densityCurve={sanitized.DensityCurve} outside finite [0,5], reverting to default {defaults.DensityCurve}");
            sanitized.DensityCurve = defaults.DensityCurve;
            rejected = true;
        }

        if (!FiniteFloatValidator.IsFiniteInRange(sanitized.PartySizeCurve, 0f, 5f))
        {
            _logger.LogWarning($"BanditScalingConfigProvider: partySizeCurve={sanitized.PartySizeCurve} outside finite [0,5], reverting to default {defaults.PartySizeCurve}");
            sanitized.PartySizeCurve = defaults.PartySizeCurve;
            rejected = true;
        }

        if (!FiniteFloatValidator.IsFiniteInRange(sanitized.BossFightCurve, 0f, 5f))
        {
            _logger.LogWarning($"BanditScalingConfigProvider: bossFightCurve={sanitized.BossFightCurve} outside finite [0,5], reverting to default {defaults.BossFightCurve}");
            sanitized.BossFightCurve = defaults.BossFightCurve;
            rejected = true;
        }

        if (sanitized.MaxHideoutsPerFactionCap < 1 || sanitized.MaxHideoutsPerFactionCap > 100)
        {
            _logger.LogWarning($"BanditScalingConfigProvider: maxHideoutsPerFactionCap={sanitized.MaxHideoutsPerFactionCap} outside [1,100], reverting to default {defaults.MaxHideoutsPerFactionCap}");
            sanitized.MaxHideoutsPerFactionCap = defaults.MaxHideoutsPerFactionCap;
            rejected = true;
        }

        if (sanitized.MaxPartiesPerHideoutCap < 1 || sanitized.MaxPartiesPerHideoutCap > 20)
        {
            _logger.LogWarning($"BanditScalingConfigProvider: maxPartiesPerHideoutCap={sanitized.MaxPartiesPerHideoutCap} outside [1,20], reverting to default {defaults.MaxPartiesPerHideoutCap}");
            sanitized.MaxPartiesPerHideoutCap = defaults.MaxPartiesPerHideoutCap;
            rejected = true;
        }

        if (sanitized.MinPartiesToInfest < 1 || sanitized.MinPartiesToInfest > sanitized.MaxPartiesPerHideoutCap)
        {
            _logger.LogWarning($"BanditScalingConfigProvider: minPartiesToInfest={sanitized.MinPartiesToInfest} outside [1,{sanitized.MaxPartiesPerHideoutCap}], reverting to default {defaults.MinPartiesToInfest}");
            sanitized.MinPartiesToInfest = defaults.MinPartiesToInfest;
            rejected = true;
        }

        if (rejected)
            _logger.LogWarning("BanditScalingConfigProvider: bandit_scaling_config.json contained invalid values. See prior warnings for details.");
        else
            _logger.LogInfo("BanditScalingConfigProvider: Loaded bandit_scaling_config.json");

        return sanitized;
    }
}
