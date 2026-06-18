using System;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;

namespace TAOM.Features.AlignmentRecruitment;

/// <summary>
/// Loads + validates <c>recruitment_alignment/recruitment_alignment_config.json</c>. Mirrors
/// <see cref="RevoltTuning.RevoltTuningConfigProvider"/>: parse failures and a semantically-invalid
/// <c>mode</c> fall back to compiled defaults with a warning (per the "Config Providers MUST
/// Validate" architecture rule). Cached for the process lifetime (Reuse.Singleton).
/// </summary>
public class RecruitmentAlignmentConfigProvider : IRecruitmentAlignmentConfigProvider
{
    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<RecruitmentAlignmentConfig> _config;

    public RecruitmentAlignmentConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _config = new Lazy<RecruitmentAlignmentConfig>(LoadConfig);
    }

    public RecruitmentAlignmentConfig GetConfig() => _config.Value;

    private RecruitmentAlignmentConfig LoadConfig()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "recruitment_alignment", "recruitment_alignment_config.json");

        if (!File.Exists(path))
        {
            _logger.LogWarning($"RecruitmentAlignmentConfigProvider: recruitment_alignment_config.json not found at {path}, using defaults");
            return new RecruitmentAlignmentConfig();
        }

        RecruitmentAlignmentConfig parsed;
        try
        {
            var json = File.ReadAllText(path);
            parsed = JsonConvert.DeserializeObject<RecruitmentAlignmentConfig>(json) ?? new RecruitmentAlignmentConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError($"RecruitmentAlignmentConfigProvider: Failed to parse recruitment_alignment_config.json: {ex.Message}");
            return new RecruitmentAlignmentConfig();
        }

        return Validate(parsed);
    }

    private RecruitmentAlignmentConfig Validate(RecruitmentAlignmentConfig parsed)
    {
        var sanitized = new RecruitmentAlignmentConfig
        {
            Enabled = parsed.Enabled,
            Mode = parsed.Mode,
            ApplyToAi = parsed.ApplyToAi,
            ApplyToPlayer = parsed.ApplyToPlayer,
        };

        var defaults = new RecruitmentAlignmentConfig();
        var rejected = false;

        if (string.Equals(sanitized.Mode, RecruitmentAlignmentConfig.ModeSymmetric, StringComparison.OrdinalIgnoreCase))
        {
            sanitized.Mode = RecruitmentAlignmentConfig.ModeSymmetric;
        }
        else if (string.Equals(sanitized.Mode, RecruitmentAlignmentConfig.ModeGoodRejectsEvil, StringComparison.OrdinalIgnoreCase))
        {
            sanitized.Mode = RecruitmentAlignmentConfig.ModeGoodRejectsEvil;
        }
        else
        {
            _logger.LogWarning($"RecruitmentAlignmentConfigProvider: mode='{sanitized.Mode}' is not '{RecruitmentAlignmentConfig.ModeSymmetric}' or '{RecruitmentAlignmentConfig.ModeGoodRejectsEvil}', reverting to default '{defaults.Mode}'");
            sanitized.Mode = defaults.Mode;
            rejected = true;
        }

        if (rejected)
            _logger.LogWarning("RecruitmentAlignmentConfigProvider: recruitment_alignment_config.json contained invalid values. See prior warnings for details.");
        else
            _logger.LogInfo("RecruitmentAlignmentConfigProvider: Loaded recruitment_alignment_config.json");

        return sanitized;
    }
}
