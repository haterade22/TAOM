using System;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;

namespace TAOM.Features.BannerColorPersistence;

public class BannerColorConfigProvider : IBannerColorConfigProvider
{
    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private BannerColorConfig _cache;

    public BannerColorConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
    }

    public BannerColorConfig GetConfig()
    {
        if (_cache != null)
            return _cache;

        var path = Path.Combine(_pathService.ConfigPath, "banner_color_config.json");

        if (!File.Exists(path))
        {
            _logger.LogWarning($"BannerColorConfigProvider: banner_color_config.json not found at {path}, using defaults");
            _cache = new BannerColorConfig();
            return _cache;
        }

        try
        {
            var json = File.ReadAllText(path);
            _cache = JsonConvert.DeserializeObject<BannerColorConfig>(json) ?? new BannerColorConfig();
            _logger.LogInfo("BannerColorConfigProvider: Loaded banner_color_config.json");
            return _cache;
        }
        catch (Exception ex)
        {
            _logger.LogError($"BannerColorConfigProvider: Failed to parse banner_color_config.json: {ex.Message}");
            _cache = new BannerColorConfig();
            return _cache;
        }
    }
}
