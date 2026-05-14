using System;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.RaceAge.Models;

namespace TAOM.Features.RaceAge;

public class RaceAgeConfigProvider : IRaceAgeConfigProvider
{
    private readonly IPathService _pathService;
    private readonly IModLogger _logger;

    public RaceAgeConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
    }

    public RaceAgeConfig LoadConfig()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "raceage", "race_age_config.json");

        if (!File.Exists(path))
        {
            _logger.LogWarning($"RaceAgeConfigProvider: race_age_config.json not found: {path}");
            return new RaceAgeConfig();
        }

        try
        {
            var json = File.ReadAllText(path);
            var config = JsonConvert.DeserializeObject<RaceAgeConfig>(json);
            // Phase 9b #131 P2 — semantic validation. Pre-fix accepted any parseable JSON; NaN
            // FertilityMod made `baseChance *= NaN` propagate (ExplainedNumber returns NaN); MaxAge
            // inversions produced negative fertility windows. Per csharp-architecture.md "Config
            // Providers MUST Validate" (R3 pattern shipped 3×).
            ValidateConfig(config);
            _logger.LogInfo($"Loaded {config.Races.Count} race age entries");
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError($"RaceAgeConfigProvider: Failed to parse race_age_config.json: {ex.Message}");
            return new RaceAgeConfig();
        }
    }

    private void ValidateConfig(RaceAgeConfig config)
    {
        if (config?.Races == null) return;

        foreach (var kvp in config.Races)
        {
            var name = kvp.Key;
            var entry = kvp.Value;
            if (entry == null) continue;

            // NaN/Infinity-guard on FertilityMod. Default to 1.0 on bad value.
            if (!FiniteFloatValidator.IsFiniteAtLeast(entry.FertilityMod, 0f))
            {
                _logger.LogWarning($"RaceAgeConfigProvider: '{name}' has invalid FertilityMod ({entry.FertilityMod}); reverting to 1.0");
                entry.FertilityMod = 1.0f;
            }

            // Ordering invariants. Inversions cause negative fertility windows → garbage rates.
            // Swap if obviously reversed (e.g. ComesOfAge=99 vs MaxAge=50); otherwise log + leave
            // (since downstream consumers tolerate small unconventional configurations).
            if (entry.ComesOfAge >= entry.FertilityEnd)
            {
                _logger.LogWarning($"RaceAgeConfigProvider: '{name}' ComesOfAge ({entry.ComesOfAge}) >= FertilityEnd ({entry.FertilityEnd}); resetting to defaults (18/45).");
                entry.ComesOfAge = 18;
                entry.FertilityEnd = 45;
            }
            if (entry.MiddleAge >= entry.MaxAge)
            {
                _logger.LogWarning($"RaceAgeConfigProvider: '{name}' MiddleAge ({entry.MiddleAge}) >= MaxAge ({entry.MaxAge}); resetting to defaults (35/85).");
                entry.MiddleAge = 35;
                entry.MaxAge = 85;
            }
            if (entry.BecomeOld >= entry.MaxAge)
            {
                _logger.LogWarning($"RaceAgeConfigProvider: '{name}' BecomeOld ({entry.BecomeOld}) >= MaxAge ({entry.MaxAge}); resetting BecomeOld to MaxAge-1.");
                entry.BecomeOld = Math.Max(entry.MiddleAge, entry.MaxAge - 1);
            }
        }
    }
}
