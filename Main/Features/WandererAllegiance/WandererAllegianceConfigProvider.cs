using System;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;

namespace TAOM.Features.WandererAllegiance;

/// <summary>
/// Loads and validates <c>wanderer_allegiance/wanderer_allegiance_config.json</c>. Mirrors
/// <see cref="MarriageAlignment.MarriageAlignmentConfigProvider"/> for the file handling (a missing
/// file or a parse failure falls back to compiled defaults with a log line) and
/// <see cref="AlignmentRecruitment.RecruitmentAlignmentConfigProvider"/> for the string field: the
/// consumer branches on <c>scope</c>, so an unknown value reverts to the default with a warning
/// rather than silently picking a mode (the "Config Providers MUST Validate" rule). Cached for the
/// process lifetime (Reuse.Singleton), so a JSON edit needs a full Bannerlord restart.
/// </summary>
public class WandererAllegianceConfigProvider : IWandererAllegianceConfigProvider
{
    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<WandererAllegianceConfig> _config;

    public WandererAllegianceConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _config = new Lazy<WandererAllegianceConfig>(LoadConfig);
    }

    public WandererAllegianceConfig GetConfig() => _config.Value;

    private WandererAllegianceConfig LoadConfig()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "wanderer_allegiance", "wanderer_allegiance_config.json");

        if (!File.Exists(path))
        {
            _logger.LogWarning($"WandererAllegianceConfigProvider: wanderer_allegiance_config.json not found at {path}, using defaults");
            return new WandererAllegianceConfig();
        }

        WandererAllegianceConfig parsed;
        try
        {
            var json = File.ReadAllText(path);
            parsed = JsonConvert.DeserializeObject<WandererAllegianceConfig>(json) ?? new WandererAllegianceConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError($"WandererAllegianceConfigProvider: Failed to parse wanderer_allegiance_config.json: {ex.Message}");
            return new WandererAllegianceConfig();
        }

        return Validate(parsed);
    }

    private WandererAllegianceConfig Validate(WandererAllegianceConfig parsed)
    {
        var defaults = new WandererAllegianceConfig();
        var sanitized = new WandererAllegianceConfig
        {
            Enabled = parsed.Enabled,
            Scope = parsed.Scope,
        };
        var rejected = false;

        if (string.Equals(sanitized.Scope, WandererAllegianceConfig.ScopeAllWanderers, StringComparison.OrdinalIgnoreCase))
        {
            sanitized.Scope = WandererAllegianceConfig.ScopeAllWanderers;
        }
        else if (string.Equals(sanitized.Scope, WandererAllegianceConfig.ScopeNamedCompanionsOnly, StringComparison.OrdinalIgnoreCase))
        {
            sanitized.Scope = WandererAllegianceConfig.ScopeNamedCompanionsOnly;
        }
        else
        {
            _logger.LogWarning(
                $"WandererAllegianceConfigProvider: scope='{sanitized.Scope}' is not '{WandererAllegianceConfig.ScopeAllWanderers}' " +
                $"or '{WandererAllegianceConfig.ScopeNamedCompanionsOnly}', reverting to default '{defaults.Scope}'");
            sanitized.Scope = defaults.Scope;
            rejected = true;
        }

        if (rejected)
            _logger.LogWarning("WandererAllegianceConfigProvider: wanderer_allegiance_config.json contained invalid values. See prior warnings for details.");
        else
            _logger.LogInfo("WandererAllegianceConfigProvider: Loaded wanderer_allegiance_config.json");

        return sanitized;
    }
}
