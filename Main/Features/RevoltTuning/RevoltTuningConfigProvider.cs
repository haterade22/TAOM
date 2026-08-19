using System;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;

namespace TAOM.Features.RevoltTuning;

public class RevoltTuningConfigProvider : IRevoltTuningConfigProvider
{
    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<RevoltTuningConfig> _config;

    public RevoltTuningConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _config = new Lazy<RevoltTuningConfig>(LoadConfig);
    }

    public RevoltTuningConfig GetConfig() => _config.Value;

    private RevoltTuningConfig LoadConfig()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "configs", "revolt_tuning_config.json");

        if (!File.Exists(path))
        {
            _logger.LogWarning($"RevoltTuningConfigProvider: revolt_tuning_config.json not found at {path}, using defaults");
            return new RevoltTuningConfig();
        }

        RevoltTuningConfig parsed;
        try
        {
            var json = File.ReadAllText(path);
            parsed = JsonConvert.DeserializeObject<RevoltTuningConfig>(json) ?? new RevoltTuningConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError($"RevoltTuningConfigProvider: Failed to parse revolt_tuning_config.json: {ex.Message}");
            return new RevoltTuningConfig();
        }

        return Validate(parsed);
    }

    private RevoltTuningConfig Validate(RevoltTuningConfig parsed)
    {
        var sanitized = new RevoltTuningConfig
        {
            RebellionStartLoyaltyThreshold = parsed.RebellionStartLoyaltyThreshold,
            RebelliousStateStartLoyaltyThreshold = parsed.RebelliousStateStartLoyaltyThreshold,
            HighRebellionStartLoyaltyThreshold = parsed.HighRebellionStartLoyaltyThreshold,
            HighRebellionRebelliousStateStartLoyaltyThreshold = parsed.HighRebellionRebelliousStateStartLoyaltyThreshold,
            SettlementOwnerDifferentCultureLoyaltyEffect = parsed.SettlementOwnerDifferentCultureLoyaltyEffect,
        };

        var defaults = new RevoltTuningConfig();
        var rejected = false;

        if (sanitized.RebellionStartLoyaltyThreshold < 0 || sanitized.RebellionStartLoyaltyThreshold > 100)
        {
            _logger.LogWarning($"RevoltTuningConfigProvider: rebellionStartLoyaltyThreshold={sanitized.RebellionStartLoyaltyThreshold} outside [0,100], reverting to default {defaults.RebellionStartLoyaltyThreshold}");
            sanitized.RebellionStartLoyaltyThreshold = defaults.RebellionStartLoyaltyThreshold;
            rejected = true;
        }

        if (sanitized.RebelliousStateStartLoyaltyThreshold < 0 || sanitized.RebelliousStateStartLoyaltyThreshold > 100)
        {
            _logger.LogWarning($"RevoltTuningConfigProvider: rebelliousStateStartLoyaltyThreshold={sanitized.RebelliousStateStartLoyaltyThreshold} outside [0,100], reverting to default {defaults.RebelliousStateStartLoyaltyThreshold}");
            sanitized.RebelliousStateStartLoyaltyThreshold = defaults.RebelliousStateStartLoyaltyThreshold;
            rejected = true;
        }

        if (sanitized.RebelliousStateStartLoyaltyThreshold < sanitized.RebellionStartLoyaltyThreshold)
        {
            _logger.LogWarning($"RevoltTuningConfigProvider: rebelliousStateStartLoyaltyThreshold ({sanitized.RebelliousStateStartLoyaltyThreshold}) must be >= rebellionStartLoyaltyThreshold ({sanitized.RebellionStartLoyaltyThreshold}), reverting both to defaults");
            sanitized.RebellionStartLoyaltyThreshold = defaults.RebellionStartLoyaltyThreshold;
            sanitized.RebelliousStateStartLoyaltyThreshold = defaults.RebelliousStateStartLoyaltyThreshold;
            rejected = true;
        }

        if (sanitized.HighRebellionStartLoyaltyThreshold < 0 || sanitized.HighRebellionStartLoyaltyThreshold > 100)
        {
            _logger.LogWarning($"RevoltTuningConfigProvider: highRebellionStartLoyaltyThreshold={sanitized.HighRebellionStartLoyaltyThreshold} outside [0,100], reverting to default {defaults.HighRebellionStartLoyaltyThreshold}");
            sanitized.HighRebellionStartLoyaltyThreshold = defaults.HighRebellionStartLoyaltyThreshold;
            rejected = true;
        }

        if (sanitized.HighRebellionRebelliousStateStartLoyaltyThreshold < 0 || sanitized.HighRebellionRebelliousStateStartLoyaltyThreshold > 100)
        {
            _logger.LogWarning($"RevoltTuningConfigProvider: highRebellionRebelliousStateStartLoyaltyThreshold={sanitized.HighRebellionRebelliousStateStartLoyaltyThreshold} outside [0,100], reverting to default {defaults.HighRebellionRebelliousStateStartLoyaltyThreshold}");
            sanitized.HighRebellionRebelliousStateStartLoyaltyThreshold = defaults.HighRebellionRebelliousStateStartLoyaltyThreshold;
            rejected = true;
        }

        if (sanitized.HighRebellionRebelliousStateStartLoyaltyThreshold < sanitized.HighRebellionStartLoyaltyThreshold)
        {
            _logger.LogWarning($"RevoltTuningConfigProvider: highRebellionRebelliousStateStartLoyaltyThreshold ({sanitized.HighRebellionRebelliousStateStartLoyaltyThreshold}) must be >= highRebellionStartLoyaltyThreshold ({sanitized.HighRebellionStartLoyaltyThreshold}), reverting both to defaults");
            sanitized.HighRebellionStartLoyaltyThreshold = defaults.HighRebellionStartLoyaltyThreshold;
            sanitized.HighRebellionRebelliousStateStartLoyaltyThreshold = defaults.HighRebellionRebelliousStateStartLoyaltyThreshold;
            rejected = true;
        }

        if (!FiniteFloatValidator.IsFiniteAtMost(sanitized.SettlementOwnerDifferentCultureLoyaltyEffect, 0f))
        {
            _logger.LogWarning($"RevoltTuningConfigProvider: settlementOwnerDifferentCultureLoyaltyEffect={sanitized.SettlementOwnerDifferentCultureLoyaltyEffect} must be a finite value <= 0 (this is a daily penalty, not a bonus), reverting to default {defaults.SettlementOwnerDifferentCultureLoyaltyEffect}");
            sanitized.SettlementOwnerDifferentCultureLoyaltyEffect = defaults.SettlementOwnerDifferentCultureLoyaltyEffect;
            rejected = true;
        }

        if (rejected)
            _logger.LogWarning("RevoltTuningConfigProvider: revolt_tuning_config.json contained invalid values. See prior warnings for details.");
        else
            _logger.LogInfo("RevoltTuningConfigProvider: Loaded revolt_tuning_config.json");

        return sanitized;
    }
}
