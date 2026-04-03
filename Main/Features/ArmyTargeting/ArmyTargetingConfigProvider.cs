using System;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;

namespace TAOM.Features.ArmyTargeting;

public class ArmyTargetingConfigProvider : IArmyTargetingConfigProvider
{
    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private ArmyTargetingConfig _cache;

    public ArmyTargetingConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
    }

    public ArmyTargetingConfig GetConfig()
    {
        if (_cache != null)
            return _cache;

        var path = Path.Combine(_pathService.ModuleDataPath, "configs", "army_targeting.json");

        if (!File.Exists(path))
        {
            _logger.LogWarning($"ArmyTargetingConfigProvider: army_targeting.json not found at {path}, using defaults");
            _cache = new ArmyTargetingConfig();
            return _cache;
        }

        try
        {
            var json = File.ReadAllText(path);
            _cache = JsonConvert.DeserializeObject<ArmyTargetingConfig>(json) ?? new ArmyTargetingConfig();
            _logger.LogInfo("ArmyTargetingConfigProvider: Loaded army_targeting.json");
            return _cache;
        }
        catch (Exception ex)
        {
            _logger.LogError($"ArmyTargetingConfigProvider: Failed to parse army_targeting.json: {ex.Message}");
            _cache = new ArmyTargetingConfig();
            return _cache;
        }
    }
}
