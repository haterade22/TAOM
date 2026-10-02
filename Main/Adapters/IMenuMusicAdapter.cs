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
}
