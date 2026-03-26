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

                config.CultureEntries.Add(new CultureResourceEntry
                {
                    CultureId = id,
                    Gold = int.Parse(el.Attribute("gold")?.Value ?? "0", CultureInfo.InvariantCulture),
                    Influence = float.Parse(el.Attribute("influence")?.Value ?? "0", CultureInfo.InvariantCulture)
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
}
