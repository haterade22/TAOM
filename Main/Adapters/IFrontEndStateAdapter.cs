namespace TAOM.Adapters;

/// <summary>Which front-end game state the player is in, and whether a loaded movie is gone (#704).</summary>
public interface IFrontEndStateAdapter
{
    /// <summary>The main menu is the active game state.</summary>
    bool IsMainMenuActive();

    /// <summary>The active game state is one vanilla plays its menu theme on: the main menu or the game-loading
    /// screen (<c>GameState.IsMusicMenuState</c>).</summary>
    bool IsMenuMusicStateActive();

    /// <summary>Character creation is on the game-state stack.</summary>
    bool IsInCharacterCreation();

    /// <summary>True when the movie handle a load returned has been released by the engine, however
    /// that happened, or is not a movie handle at all.</summary>
    bool IsMovieReleased(object movie);
}
