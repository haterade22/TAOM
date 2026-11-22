namespace TAOM.Features.TrainingGrounds.Teams;

/// <summary>
/// Reads the MCM switch live. <c>TaomSettings.Instance</c> is null very early in startup or when MCM fails to
/// load; the team fight is on by default, so that reads as enabled. The instance is taken once, through a lazy
/// accessor, as the other providers do (<c>CampaignHotPathSettingsProvidersTests</c>).
/// </summary>
public sealed class TeamPracticeSettingsProvider : ITeamPracticeSettingsProvider
{
    private TaomSettings? _settings;

    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public bool IsEnabled => From(Settings);

    internal static bool From(TaomSettings? settings) => settings?.EnableTeamPracticeFights ?? true;
}
