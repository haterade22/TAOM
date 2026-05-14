using System;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.Diplomacy.Models;

namespace TAOM.Features.Diplomacy;

public class DiplomacyConfigProvider : IDiplomacyConfigProvider
{
    private readonly IPathService _pathService;
    private readonly IModLogger _logger;

    public DiplomacyConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
    }

    public DiplomacyConfig LoadConfig()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "diplomacy", "diplomacy.json");

        if (!File.Exists(path))
        {
            _logger.LogWarning($"DiplomacyConfigProvider: diplomacy.json not found: {path}");
            return new DiplomacyConfig();
        }

        try
        {
            var json = File.ReadAllText(path);
            // Phase 9b #129 P2 — null-literal JSON returns null; `.Relationships` would NRE.
            var config = JsonConvert.DeserializeObject<DiplomacyConfig>(json) ?? new DiplomacyConfig();
            _logger.LogInfo($"Loaded {config.Relationships.Count} diplomacy relationships");
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError($"DiplomacyConfigProvider: Failed to parse diplomacy.json: {ex.Message}");
            return new DiplomacyConfig();
        }
    }
}
