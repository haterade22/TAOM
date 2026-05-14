using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.StartupResources.Config;

namespace TAOM.Features.StartupResources;

public class StartupResourcesConfigProvider : IStartupResourcesConfigProvider
{
    private const int PlayerGoldMaxValue = 10_000_000;

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private StartupResourcesConfig _cached;

    public StartupResourcesConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
    }

    public StartupResourcesConfig LoadConfig()
    {
        if (_cached != null)
            return _cached;

        var path = Path.Combine(_pathService.ModuleDataPath, "startup_resources", "startup_resources_config.xml");

        if (!File.Exists(path))
        {
            _logger.LogWarning($"StartupResourcesConfigProvider: Config file not found: {path}");
            _cached = new StartupResourcesConfig();
            return _cached;
        }

        try
        {
            var doc = XDocument.Load(path);
            var config = new StartupResourcesConfig();

            foreach (var el in doc.Root.Elements("Culture"))
            {
                var id = el.Attribute("id")?.Value;
                if (string.IsNullOrEmpty(id))
                    continue;

                // Phase 9b #136 P1 — validate Gold (int, must be ≥ 0) and Influence (float, must be
                // finite + ≥ 0). Pre-fix `int.Parse("-500000")` flowed to GiveGoldToHero(-500000)
                // unchallenged; `float.Parse("NaN")` returned NaN, which the downstream `> 0f`
                // guard rejected silently with no warning. Now uses the same TryParse-and-validate
                // pattern as ParsePlayerGold.
                config.CultureEntries.Add(new CultureResourceEntry
                {
                    CultureId = id,
                    Gold = ParseGold(el.Attribute("gold")?.Value, id),
                    Influence = ParseInfluence(el.Attribute("influence")?.Value, id),
                    PlayerGold = ParsePlayerGold(el.Attribute("playerGold")?.Value, id)
                });
            }

            _cached = config;
            return _cached;
        }
        catch (Exception ex)
        {
            _logger.LogError($"StartupResourcesConfigProvider: Failed to parse {path}: {ex.Message}");
            _cached = new StartupResourcesConfig();
            return _cached;
        }
    }

    private int ParsePlayerGold(string raw, string cultureId)
    {
        if (string.IsNullOrEmpty(raw))
            return 0;

        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            _logger.LogWarning($"StartupResourcesConfigProvider: invalid playerGold='{raw}' for culture '{cultureId}' — reverting to 0");
            return 0;
        }

        if (value < 0 || value > PlayerGoldMaxValue)
        {
            _logger.LogWarning($"StartupResourcesConfigProvider: playerGold={value} for culture '{cultureId}' out of range [0, {PlayerGoldMaxValue}] — reverting to 0");
            return 0;
        }

        return value;
    }

    // Phase 9b #136 P1 — validate Gold (int, ≥ 0). Pre-fix int.Parse("-500000") passed unchallenged.
    private int ParseGold(string raw, string cultureId)
    {
        if (string.IsNullOrEmpty(raw)) return 0;

        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            _logger.LogWarning($"StartupResourcesConfigProvider: invalid gold='{raw}' for culture '{cultureId}' — reverting to 0");
            return 0;
        }
        if (value < 0)
        {
            _logger.LogWarning($"StartupResourcesConfigProvider: gold={value} for culture '{cultureId}' negative — reverting to 0");
            return 0;
        }
        return value;
    }

    // Phase 9b #136 P1 — validate Influence (float, finite + ≥ 0). Pre-fix float.Parse("NaN")
    // returned NaN; downstream `entry.Influence > 0f` evaluated false silently.
    private float ParseInfluence(string raw, string cultureId)
    {
        if (string.IsNullOrEmpty(raw)) return 0f;

        if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            _logger.LogWarning($"StartupResourcesConfigProvider: invalid influence='{raw}' for culture '{cultureId}' — reverting to 0");
            return 0f;
        }
        if (!TAOM.Core.Validation.FiniteFloatValidator.IsFiniteAtLeast(value, 0f))
        {
            _logger.LogWarning($"StartupResourcesConfigProvider: influence={value} for culture '{cultureId}' invalid (NaN/Inf/negative) — reverting to 0");
            return 0f;
        }
        return value;
    }
}
