namespace TAOM.Features.CheatScreenGuard;

/// <summary>The MCM toggle of the cheat-screen guard (Battle Load Diagnostics page, group "Map Performance").</summary>
public interface ICheatScreenGuardSettingsProvider
{
    /// <summary>"Cheat Mode Lists All Items And Troops". Read at each screen open while cheat mode is on.</summary>
    bool CheatListsEnabled { get; }
}
