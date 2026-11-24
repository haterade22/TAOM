namespace TAOM.Adapters;

/// <summary>
/// Sets the engine music manager's mode without the theme change vanilla's own menu-mode methods
/// would make (#704: the themed main menu's video carries its own audio).
/// </summary>
public interface IMenuMusicAdapter
{
    /// <summary>Marks the menu theme as playing without starting it (<paramref name="musicManager"/> is
    /// the engine's music manager, as the patch received it).</summary>
    void MarkMenuModeWithoutTheme(object musicManager);

    /// <summary>Puts <paramref name="musicManager"/> (null: the current one) back to paused, so vanilla's
    /// next update starts the music its state calls for.</summary>
    void MarkPaused(object? musicManager);

    /// <summary>The current music manager is in menu mode, so vanilla's menu theme is playing (or, under
    /// our silence, marked as playing).</summary>
    bool IsMenuThemePlaying();

    /// <summary>Runs vanilla's private <c>DeactivateMenuMode</c> on the current music manager: the menu
    /// theme stops and the mode goes back to paused (v1.5.7 MBMusicManager.cs:445-449: <c>PsaiCore.MenuModeLeave()</c>,
    /// then <c>CurrentMode = Paused</c>), so vanilla's next update enters menu mode again.</summary>
    void LeaveMenuMode();
}
