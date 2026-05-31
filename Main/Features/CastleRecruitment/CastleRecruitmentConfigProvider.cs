using System;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;

namespace TAOM.Features.CastleRecruitment;

/// <summary>
/// Loads <c>castle_recruitment/castle_recruitment_config.json</c> with semantic validation. Mirrors
/// <c>BanditScalingConfigProvider</c>: missing/malformed file falls back to compiled defaults; an
/// out-of-range <c>NotablesPerCastle</c> reverts to default with a logged warning (parse success is
/// not validation success).
/// </summary>
public class CastleRecruitmentConfigProvider : ICastleRecruitmentConfigProvider
{
    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<CastleRecruitmentConfig> _config;

    public CastleRecruitmentConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _config = new Lazy<CastleRecruitmentConfig>(LoadConfig);
    }

    public CastleRecruitmentConfig GetConfig() => _config.Value;

    private CastleRecruitmentConfig LoadConfig()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "castle_recruitment", "castle_recruitment_config.json");

        if (!File.Exists(path))
        {
            _logger.LogWarning($"CastleRecruitmentConfigProvider: castle_recruitment_config.json not found at {path}, using defaults");
            return new CastleRecruitmentConfig();
        }

        CastleRecruitmentConfig parsed;
        try
        {
            var json = File.ReadAllText(path);
            parsed = JsonConvert.DeserializeObject<CastleRecruitmentConfig>(json) ?? new CastleRecruitmentConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError($"CastleRecruitmentConfigProvider: Failed to parse castle_recruitment_config.json: {ex.Message}");
            return new CastleRecruitmentConfig();
        }

        return Validate(parsed);
    }

    private CastleRecruitmentConfig Validate(CastleRecruitmentConfig parsed)
    {
        var sanitized = new CastleRecruitmentConfig
        {
            Enabled = parsed.Enabled,
            AiEnabled = parsed.AiEnabled,
            NotablesPerCastle = parsed.NotablesPerCastle,
        };

        var defaults = new CastleRecruitmentConfig();
        var rejected = false;

        if (sanitized.NotablesPerCastle < 1 || sanitized.NotablesPerCastle > 5)
        {
            _logger.LogWarning($"CastleRecruitmentConfigProvider: notablesPerCastle={sanitized.NotablesPerCastle} outside [1,5], reverting to default {defaults.NotablesPerCastle}");
            sanitized.NotablesPerCastle = defaults.NotablesPerCastle;
            rejected = true;
        }

        if (rejected)
            _logger.LogWarning("CastleRecruitmentConfigProvider: castle_recruitment_config.json contained invalid values. See prior warnings for details.");
        else
            _logger.LogInfo("CastleRecruitmentConfigProvider: Loaded castle_recruitment_config.json");

        return sanitized;
    }
}
