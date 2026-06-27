using System;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;

namespace TAOM.Features.AlignmentDesertion;

/// <summary>
/// Loads + validates <c>alignment_desertion/alignment_desertion_config.json</c>. Mirrors
/// <see cref="AlignmentRecruitment.RecruitmentAlignmentConfigProvider"/>: parse failures and a
/// semantically-invalid <c>Rate</c> fall back to the compiled default with a warning (per the
/// "Config Providers MUST Validate" architecture rule — <c>Rate</c> is a float, so NaN/Infinity must
/// be rejected BEFORE the range check via <see cref="FiniteFloatValidator"/>). Cached for the
/// process lifetime (Reuse.Singleton) — edits require an app restart.
/// </summary>
public class AlignmentDesertionConfigProvider : IAlignmentDesertionConfigProvider
{
    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<AlignmentDesertionConfig> _config;

    public AlignmentDesertionConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _config = new Lazy<AlignmentDesertionConfig>(LoadConfig);
    }

    public AlignmentDesertionConfig GetConfig() => _config.Value;

    private AlignmentDesertionConfig LoadConfig()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "alignment_desertion", "alignment_desertion_config.json");

        if (!File.Exists(path))
        {
            _logger.LogWarning($"AlignmentDesertionConfigProvider: alignment_desertion_config.json not found at {path}, using defaults");
            return new AlignmentDesertionConfig();
        }

        AlignmentDesertionConfig parsed;
        try
        {
            var json = File.ReadAllText(path);
            parsed = JsonConvert.DeserializeObject<AlignmentDesertionConfig>(json) ?? new AlignmentDesertionConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError($"AlignmentDesertionConfigProvider: Failed to parse alignment_desertion_config.json: {ex.Message}");
            return new AlignmentDesertionConfig();
        }

        return Validate(parsed);
    }

    private AlignmentDesertionConfig Validate(AlignmentDesertionConfig parsed)
    {
        var defaults = new AlignmentDesertionConfig();
        var sanitized = new AlignmentDesertionConfig
        {
            Enabled = parsed.Enabled,
            Rate = parsed.Rate,
            ApplyToAi = parsed.ApplyToAi,
            ApplyToPlayer = parsed.ApplyToPlayer,
            ApplyToParties = parsed.ApplyToParties,
            ApplyToGarrisons = parsed.ApplyToGarrisons,
        };

        var rejected = false;

        // Rate is a float — NaN/Infinity must be rejected before the [0,1] range check (IEEE-754).
        if (!FiniteFloatValidator.IsFiniteInRange(sanitized.Rate, 0f, 1f))
        {
            _logger.LogWarning($"AlignmentDesertionConfigProvider: rate='{sanitized.Rate}' is not finite within [0,1], reverting to default {defaults.Rate}");
            sanitized.Rate = defaults.Rate;
            rejected = true;
        }

        if (rejected)
            _logger.LogWarning("AlignmentDesertionConfigProvider: alignment_desertion_config.json contained invalid values. See prior warnings for details.");
        else
            _logger.LogInfo("AlignmentDesertionConfigProvider: Loaded alignment_desertion_config.json");

        return sanitized;
    }
}
