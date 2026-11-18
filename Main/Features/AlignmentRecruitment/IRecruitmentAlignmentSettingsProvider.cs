namespace TAOM.Features.AlignmentRecruitment;

/// <summary>
/// Live (MCM-over-JSON) settings surface for the recruitment-alignment gate. Lets the pure
/// <see cref="RecruitmentAlignmentService"/> stay free of MCM + JSON plumbing.
/// </summary>
public interface IRecruitmentAlignmentSettingsProvider
{
    bool IsEnabled { get; }

    /// <summary>What a new campaign gets for the master switch when its alignment preset is Standard: MCM, else JSON.</summary>
    bool NewCampaignDefault { get; }
    bool ApplyToAi { get; }
    bool ApplyToPlayer { get; }
    bool GoodRejectsEvilOnly { get; }
}
