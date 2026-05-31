namespace TAOM.Features.CastleRecruitment;

/// <summary>
/// JSON-deserialized defaults for the Castle Recruitment feature. MCM knobs (see
/// <c>TaomSettings</c>) override these at runtime via <see cref="CastleRecruitmentSettingsProvider"/>.
/// </summary>
public class CastleRecruitmentConfig
{
    /// <summary>Master toggle. When false the whole feature is inert (no castle notables spawned,
    /// no menu option, no AI castle recruitment, issues not suppressed).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>When true, AI lord parties also score, travel to, and recruit from castles
    /// (the two AI-scoring transpilers + the in-settlement recruit postfix). Independent of the
    /// player-facing path, but gated under <see cref="Enabled"/>.</summary>
    public bool AiEnabled { get; set; } = true;

    /// <summary>How many notables each castle is maintained at (distributed round-robin across
    /// GangLeader/Headman/Merchant/Artisan). Vanilla towns = 5, villages = 3; moderate default = 3.</summary>
    public int NotablesPerCastle { get; set; } = 3;
}
