namespace TAOM.Features.AlignmentDesertion;

/// <summary>
/// JSON DTO for <c>alignment_desertion/alignment_desertion_config.json</c>. Validated on load by
/// <see cref="AlignmentDesertionConfigProvider"/>; MCM (<c>TaomSettings</c>) overrides these at
/// runtime via <see cref="AlignmentDesertionSettingsProvider"/>.
/// </summary>
public class AlignmentDesertionConfig
{
    /// <summary>Master toggle. When false, no troops ever desert by alignment.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Fraction (0..1) of each opposed-alignment troop type that deserts per day. Default 0.5.</summary>
    public float Rate { get; set; } = 0.5f;

    /// <summary>When false, AI-owned parties/garrisons never shed opposed troops.</summary>
    public bool ApplyToAi { get; set; } = true;

    /// <summary>When false, the player's own party/garrison never sheds opposed troops.</summary>
    public bool ApplyToPlayer { get; set; } = true;

    /// <summary>When false, mobile parties are not affected (garrisons may still be).</summary>
    public bool ApplyToParties { get; set; } = true;

    /// <summary>When false, settlement garrisons are not affected (mobile parties may still be).</summary>
    public bool ApplyToGarrisons { get; set; } = true;
}
