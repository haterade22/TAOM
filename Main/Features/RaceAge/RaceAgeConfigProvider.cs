using System;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
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
            _logger.LogInfo($"Loaded {config.Races.Count} race age entries");
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError($"RaceAgeConfigProvider: Failed to parse race_age_config.json: {ex.Message}");
            return new RaceAgeConfig();
        }
    }
}
