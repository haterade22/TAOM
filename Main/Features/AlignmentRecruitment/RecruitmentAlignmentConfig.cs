using System;
using Newtonsoft.Json;

namespace TAOM.Features.AlignmentRecruitment;

/// <summary>
/// JSON DTO for <c>recruitment_alignment/recruitment_alignment_config.json</c>. Validated on load
/// by <see cref="RecruitmentAlignmentConfigProvider"/>; MCM (<c>TaomSettings</c>) overrides these
/// at runtime via <see cref="RecruitmentAlignmentSettingsProvider"/>.
/// </summary>
public class RecruitmentAlignmentConfig
{
    /// <summary>Master toggle. When false, recruitment is never blocked.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// "Symmetric" (default) blocks any cross-alignment recruit (Free↔Evil both ways).
    /// "GoodRejectsEvil" blocks only a Free recruiter from an Evil-controlled source.
    /// Validated to one of these two values on load; unknown reverts to "Symmetric".
    /// </summary>
    public string Mode { get; set; } = ModeSymmetric;

    /// <summary>When false, only the player is gated; AI lords recruit unrestricted.</summary>
    public bool ApplyToAi { get; set; } = true;

    public const string ModeSymmetric = "Symmetric";
    public const string ModeGoodRejectsEvil = "GoodRejectsEvil";

    /// <summary>
    /// True when <see cref="Mode"/> is "GoodRejectsEvil" (case-insensitive). Derived, not serialized.
    /// </summary>
    [JsonIgnore]
    public bool GoodRejectsEvilOnly =>
        string.Equals(Mode, ModeGoodRejectsEvil, StringComparison.OrdinalIgnoreCase);
}
