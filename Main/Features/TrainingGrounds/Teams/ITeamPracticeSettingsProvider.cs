namespace TAOM.Features.TrainingGrounds.Teams;

/// <summary>The MCM switch for the team practice fight (#1528), read live.</summary>
public interface ITeamPracticeSettingsProvider
{
    bool IsEnabled { get; }
}
