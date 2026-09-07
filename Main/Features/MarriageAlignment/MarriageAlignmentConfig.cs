namespace TAOM.Features.MarriageAlignment;

/// <summary>
/// JSON DTO for <c>marriage_alignment/marriage_alignment_config.json</c>. Loaded by
/// <see cref="MarriageAlignmentConfigProvider"/>; MCM (<c>TaomSettings</c>) overrides these at
/// runtime via <see cref="MarriageAlignmentSettingsProvider"/>.
/// </summary>
/// <remarks>
/// Every field is a bool, so there is no semantically-invalid-but-parseable value to reject (a bool
/// has no invalid state). The provider still emits the mandated load summary line. If a numeric or
/// string field is ever added here, the "Config Providers MUST Validate" rule applies to it in full.
/// </remarks>
public class MarriageAlignmentConfig
{
    /// <summary>Master toggle. When false, no marriage is ever blocked and the AI draw stays vanilla.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>When false, AI lords marry unrestricted (the player is still gated if <see cref="ApplyToPlayer"/>).</summary>
    public bool ApplyToAi { get; set; } = true;

    /// <summary>When false, the player clan marries unrestricted (AI lords are still gated if <see cref="ApplyToAi"/>).</summary>
    public bool ApplyToPlayer { get; set; } = true;

    /// <summary>
    /// Narrows the AI's random partner-clan draw to compatible clans. Vanilla draws uniformly from
    /// every clan in the campaign, so without this a blocked Free lord simply wastes the day's draw
    /// and Free clans marry far less often. Turning this off leaves the block in place but restores
    /// vanilla's draw.
    /// </summary>
    public bool SteerAiPartnerSearch { get; set; } = true;
}
