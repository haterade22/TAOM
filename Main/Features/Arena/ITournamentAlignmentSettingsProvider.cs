namespace TAOM.Features.Arena;

/// <summary>The MCM switch for the tournament alignment filter (#744); a seam so the service test can flip it.</summary>
public interface ITournamentAlignmentSettingsProvider
{
    bool IsEnabled { get; }
}
