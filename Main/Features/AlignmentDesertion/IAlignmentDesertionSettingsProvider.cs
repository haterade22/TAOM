namespace TAOM.Features.AlignmentDesertion;

/// <summary>
/// Live (MCM-over-JSON) settings surface for alignment desertion. Lets the pure
/// <see cref="AlignmentDesertionService"/> stay free of MCM + JSON plumbing.
/// </summary>
public interface IAlignmentDesertionSettingsProvider
{
    bool IsEnabled { get; }

    /// <summary>What a new campaign gets for the master switch when its alignment preset is Standard: MCM, else JSON.</summary>
    bool NewCampaignDefault { get; }
    float Rate { get; }
    bool ApplyToAi { get; }
    bool ApplyToPlayer { get; }
    bool ApplyToParties { get; }
    bool ApplyToGarrisons { get; }

    /// <summary>
    /// The Recruitment Alignment "Only Good Rejects Evil" switch. When on, Evil owners keep Free troops, matching
    /// the recruiting rule that lets Evil recruiters take them.
    /// </summary>
    bool GoodRejectsEvilOnly { get; }
}
