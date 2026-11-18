namespace TAOM.Features.MarriageAlignment;

/// <summary>
/// Live (MCM-over-JSON) settings surface for the marriage-alignment gate. Lets the pure
/// <see cref="MarriageAlignmentService"/> stay free of MCM + JSON plumbing.
/// </summary>
public interface IMarriageAlignmentSettingsProvider
{
    bool IsEnabled { get; }

    /// <summary>What a new campaign gets for the master switch when its alignment preset is Standard: MCM, else JSON.</summary>
    bool NewCampaignDefault { get; }
    bool ApplyToAi { get; }
    bool ApplyToPlayer { get; }
    bool SteerAiPartnerSearch { get; }
}
