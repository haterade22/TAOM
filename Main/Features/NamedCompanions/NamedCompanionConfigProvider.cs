using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.NamedCompanions.Domain;

namespace TAOM.Features.NamedCompanions;

public class NamedCompanionConfigProvider : INamedCompanionConfigProvider
{
    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private IReadOnlyList<NamedCompanionDefinition> _cache;

    public NamedCompanionConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
    }

    public IReadOnlyList<NamedCompanionDefinition> GetCompanions()
    {
        if (_cache != null)
            return _cache;

        var path = Path.Combine(_pathService.ModuleDataPath, "named_companions", "named_companion_config.json");

        if (!File.Exists(path))
        {
            _logger.LogWarning($"NamedCompanionConfigProvider: Config not found at {path}");
            _cache = Array.Empty<NamedCompanionDefinition>();
            return _cache;
        }

        try
        {
            var json = File.ReadAllText(path);
            var list = JsonConvert.DeserializeObject<List<NamedCompanionDefinition>>(json);
            _cache = (list ?? new List<NamedCompanionDefinition>()).AsReadOnly();
            _logger.LogInfo($"NamedCompanionConfigProvider: Loaded {_cache.Count} named companions");
            return _cache;
        }
        catch (Exception ex)
        {
            _logger.LogError($"NamedCompanionConfigProvider: Failed to parse named_companion_config.json: {ex.Message}");
            _cache = Array.Empty<NamedCompanionDefinition>();
            return _cache;
        }
    }
}
