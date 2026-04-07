using System;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.Siege.Models;

namespace TAOM.Features.Siege;

public class SiegeDefenseConfigProvider : ISiegeDefenseConfigProvider
{
    private readonly IPathService _pathService;
    private readonly IModLogger _logger;

    public SiegeDefenseConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
    }

    public SiegeDefenseConfig LoadConfig()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "siege", "siege_defense_config.json");

        if (!File.Exists(path))
        {
            _logger.LogWarning($"SiegeDefenseConfigProvider: siege_defense_config.json not found: {path}");
            return new SiegeDefenseConfig();
        }

        try
        {
            var json = File.ReadAllText(path);
            var config = JsonConvert.DeserializeObject<SiegeDefenseConfig>(json) ?? new SiegeDefenseConfig();
            _logger.LogInfo($"[SiegeDefense] Loaded config: {config.KingdomMessages.Count} kingdom messages, {config.WatchedSettlementIds.Count} watched settlements");
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError($"SiegeDefenseConfigProvider: Failed to parse siege_defense_config.json: {ex.Message}");
            return new SiegeDefenseConfig();
        }
    }
}
