namespace TAOM.Features.AlignmentRecruitment;

/// <summary>
/// Live (MCM-over-JSON) settings surface for the recruitment-alignment gate. Lets the pure
/// <see cref="RecruitmentAlignmentService"/> stay free of MCM + JSON plumbing.
/// </summary>
public interface IRecruitmentAlignmentSettingsProvider
{
    bool IsEnabled { get; }
    bool ApplyToAi { get; }
    bool GoodRejectsEvilOnly { get; }
}
