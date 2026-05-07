using System;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;

namespace TAOM.Features.Messengers;

public class MessengerConfigProvider : IMessengerConfigProvider
{
    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<MessengerConfig> _config;

    public MessengerConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _config = new Lazy<MessengerConfig>(LoadConfig);
    }

    public MessengerConfig GetConfig() => _config.Value;

    private MessengerConfig LoadConfig()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "messengers", "messenger_config.json");

        if (!File.Exists(path))
        {
            _logger.LogInfo($"MessengerConfigProvider: messenger_config.json not found at {path}, using defaults");
            return new MessengerConfig();
        }

        MessengerConfig parsed;
        try
        {
            var json = File.ReadAllText(path);
            parsed = JsonConvert.DeserializeObject<MessengerConfig>(json) ?? new MessengerConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError($"MessengerConfigProvider: Failed to parse messenger_config.json: {ex.Message}");
            return new MessengerConfig();
        }

        return Validate(parsed);
    }

    private MessengerConfig Validate(MessengerConfig parsed)
    {
        var sanitized = new MessengerConfig
        {
            AccidentChancePerHour = parsed.AccidentChancePerHour,
            TravelSpeedMultiplier = parsed.TravelSpeedMultiplier,
        };

        var defaults = new MessengerConfig();
        var rejected = false;

        // Codex review #34: '<' and '>' both fail for NaN, so an `accidentChancePerHour: NaN` would
        // sneak past the range check and propagate NaN into RollAccident (silently false) and
        // CalculateMessengerSpeed (poison NaN positions, prevent arrival). Reject non-finite up front.
        if (float.IsNaN(sanitized.AccidentChancePerHour) || float.IsInfinity(sanitized.AccidentChancePerHour)
            || sanitized.AccidentChancePerHour < 0f || sanitized.AccidentChancePerHour > 1f)
        {
            _logger.LogWarning($"MessengerConfigProvider: accidentChancePerHour={sanitized.AccidentChancePerHour} not finite or outside [0,1], reverting to default {defaults.AccidentChancePerHour}");
            sanitized.AccidentChancePerHour = defaults.AccidentChancePerHour;
            rejected = true;
        }

        if (float.IsNaN(sanitized.TravelSpeedMultiplier) || float.IsInfinity(sanitized.TravelSpeedMultiplier)
            || sanitized.TravelSpeedMultiplier < 0.1f || sanitized.TravelSpeedMultiplier > 10f)
        {
            _logger.LogWarning($"MessengerConfigProvider: travelSpeedMultiplier={sanitized.TravelSpeedMultiplier} not finite or outside [0.1,10], reverting to default {defaults.TravelSpeedMultiplier}");
            sanitized.TravelSpeedMultiplier = defaults.TravelSpeedMultiplier;
            rejected = true;
        }

        if (rejected)
            _logger.LogWarning("MessengerConfigProvider: messenger_config.json contained invalid values. See prior warnings for details.");
        else
            _logger.LogInfo("MessengerConfigProvider: Loaded messenger_config.json");

        return sanitized;
    }
}
