namespace TAOM.Features.WarChronicle.Rally;

/// <summary>
/// <c>war_chronicle/rally.json</c> (docs/features/war-chronicle.md, Part C). The property initialisers
/// are the compiled defaults, which the provider falls back to field by field. Mutable only because
/// Newtonsoft fills it; <see cref="RallyConfigProvider"/> hands out one validated instance.
/// </summary>
public sealed class RallyConfig
{
    /// <summary>Master switch in the data file; the MCM <c>WarRallyEnabled</c> must also be on.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Whether a Neutral-side kingdom can receive the effects.</summary>
    public bool IncludeNeutral { get; set; } = true;

    /// <summary>
    /// How long a written effect lasts, in campaign hours (24 to 168). The rally refreshes it every day
    /// while the tier holds, before that day's escape roll, so the lifetime matters only on a tick where
    /// the rally does not run. A daily tick can fire a little later than 24 hours after the last one, so
    /// 24 covers no such tick; the default 48 covers one.
    /// </summary>
    public float EffectTtlHours { get; set; } = 48f;

    public RallyTierConfig Tier1 { get; set; } = new RallyTierConfig
    {
        EnterLoss = 0.25f, ExitLoss = 0.15f, VolunteerRate = 0.10f, PrisonerEscape = 0.50f,
    };

    public RallyTierConfig Tier2 { get; set; } = new RallyTierConfig
    {
        EnterLoss = 0.50f, ExitLoss = 0.40f, VolunteerRate = 0.20f, PrisonerEscape = 1.00f,
    };
}

/// <summary>One catch-up level: the loss that enters it, the lower loss that leaves it, and what it grants.</summary>
public sealed class RallyTierConfig
{
    /// <summary>Share of the baseline fortification points lost at which the tier starts (0 to 1, exclusive).</summary>
    public float EnterLoss { get; set; }

    /// <summary>Share lost below which the tier ends; under <see cref="EnterLoss"/>, so the tier does not flap.</summary>
    public float ExitLoss { get; set; }

    /// <summary>Added to the volunteer-rate sum (0.10 is +10%), before the MCM strength.</summary>
    public float VolunteerRate { get; set; }

    /// <summary>Added to the prisoner-escape sum (0.50 is +50% on the daily escape roll), before the MCM strength.</summary>
    public float PrisonerEscape { get; set; }
}
