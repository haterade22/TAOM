namespace TAOM.Features.Arena;

/// <summary>
/// Reads the MCM toggle live. <c>TaomSettings.Instance</c> is null very early in startup or when MCM
/// fails to load; the filter is on by default, so that reads as enabled.
/// </summary>
public sealed class TournamentAlignmentSettingsProvider : ITournamentAlignmentSettingsProvider
{
    public bool IsEnabled => TaomSettings.Instance?.TournamentKeepEnemySidesOut ?? true;
}
