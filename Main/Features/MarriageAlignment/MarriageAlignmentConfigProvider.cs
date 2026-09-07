using System;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;

namespace TAOM.Features.MarriageAlignment;

/// <summary>
/// Loads <c>marriage_alignment/marriage_alignment_config.json</c>. Mirrors
/// <see cref="AlignmentRecruitment.RecruitmentAlignmentConfigProvider"/>: a missing file or a parse
/// failure falls back to compiled defaults with a log line, and the load ends in the mandated
/// summary line (per the "Config Providers MUST Validate" architecture rule). Cached for the
/// process lifetime (Reuse.Singleton), so a JSON edit needs a full Bannerlord restart.
/// </summary>
public class MarriageAlignmentConfigProvider : IMarriageAlignmentConfigProvider
{
    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<MarriageAlignmentConfig> _config;

    public MarriageAlignmentConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _config = new Lazy<MarriageAlignmentConfig>(LoadConfig);
    }

    public MarriageAlignmentConfig GetConfig() => _config.Value;

    private MarriageAlignmentConfig LoadConfig()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "marriage_alignment", "marriage_alignment_config.json");

        if (!File.Exists(path))
        {
            _logger.LogWarning($"MarriageAlignmentConfigProvider: marriage_alignment_config.json not found at {path}, using defaults");
            return new MarriageAlignmentConfig();
        }

        MarriageAlignmentConfig parsed;
        try
        {
            var json = File.ReadAllText(path);
            parsed = JsonConvert.DeserializeObject<MarriageAlignmentConfig>(json) ?? new MarriageAlignmentConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError($"MarriageAlignmentConfigProvider: Failed to parse marriage_alignment_config.json: {ex.Message}");
            return new MarriageAlignmentConfig();
        }

        // Every field is a bool, so there is nothing parseable-but-invalid to reject. The summary
        // line still fires so the load is visible in the log alongside every other config provider.
        _logger.LogInfo("MarriageAlignmentConfigProvider: Loaded marriage_alignment_config.json");
        return parsed;
    }
}
