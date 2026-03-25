using System;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.Diplomacy.Models;

namespace TAOM.Features.Diplomacy;

public class WarOfTheRingConfigProvider : IWarOfTheRingConfigProvider
{
    private readonly IPathService _pathService;
    private readonly IModLogger _logger;

    public WarOfTheRingConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
    }

    public WarOfTheRingConfig LoadConfig()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "diplomacy", "war_of_the_ring.json");

        if (!File.Exists(path))
        {
            _logger.LogWarning($"WarOfTheRingConfigProvider: war_of_the_ring.json not found: {path}");
            return new WarOfTheRingConfig();
        }

        try
        {
            var json = File.ReadAllText(path);
            var config = JsonConvert.DeserializeObject<WarOfTheRingConfig>(json);
            _logger.LogInfo($"Loaded War of the Ring config: Phase1 day {config.Phase1.TriggerDay}, Phase2 day {config.Phase2.TriggerDay}");
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError($"WarOfTheRingConfigProvider: Failed to parse war_of_the_ring.json: {ex.Message}");
            return new WarOfTheRingConfig();
        }
    }
}
