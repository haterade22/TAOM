namespace TAOM.Adapters;

/// <summary>Which front-end game state the player is in, and whether a loaded movie is gone (#704).</summary>
public interface IFrontEndStateAdapter
{
    /// <summary>The main menu is the active game state.</summary>
    bool IsMainMenuActive();

    /// <summary>The active game state is the main menu or the game-loading screen. Not
    /// <c>GameState.IsMusicMenuState</c>: Custom Battle setup also sets it, and its theme must play.</summary>
    bool IsMainMenuOrLoadingActive();

    /// <summary>Character creation is on the game-state stack.</summary>
    bool IsInCharacterCreation();

    /// <summary>True when the movie handle a load returned has been released by the engine, however
    /// that happened, or is not a movie handle at all.</summary>
    bool IsMovieReleased(object movie);
}
