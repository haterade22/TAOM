namespace TAOM.Features.PrisonerRecruitment;

public interface IPrisonerRecruitmentSettingsProvider
{
    bool IsEnabled { get; }
    bool ApplyToPlayer { get; }
    bool ApplyToAi { get; }
}
