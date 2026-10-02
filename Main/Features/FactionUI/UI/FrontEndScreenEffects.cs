using TaleWorlds.Engine.GauntletUI;
using TAOM.Features.FactionUI.CharacterCreation;
using TAOM.Features.FactionUI.Menus;

namespace TAOM.Features.FactionUI.UI;

/// <summary>
/// What happens after a movie finishes loading (#704), from the postfix of Kysaro's
/// <c>MainMenu_LoadMovie_Patch</c>: the character-creation camera follows the stage, and the themed main
/// menu and face generator get their live effects, which let go once the engine releases their movie.
/// Also ticks every themed screen's animations, with a sanitized frame time.
/// </summary>
public sealed class FrontEndScreenEffects
{
    private readonly FaceGenCameraService _camera;
    private readonly MainMenuWidgets _mainMenu;
    private readonly CharacterCreationWidgets _characterCreation;
    private readonly FactionScreenWidgets _factionScreen;

    public FrontEndScreenEffects(
        FaceGenCameraService camera,
        MainMenuWidgets mainMenu,
        CharacterCreationWidgets characterCreation,
        FactionScreenWidgets factionScreen)
    {
        _camera = camera;
        _mainMenu = mainMenu;
        _characterCreation = characterCreation;
        _factionScreen = factionScreen;
    }

    /// <summary>A movie was built on <paramref name="layer"/>; <paramref name="movie"/> is the engine's
    /// handle to it.</summary>
    public void OnMovieLoaded(GauntletLayer layer, object movie, string? movieName, object? dataSource)
    {
        _camera.OnMovieLoaded(movieName);
        if (movieName == FrontEndMovieService.MainMenuMovie)
            _mainMenu.Attach(layer, movie);
        else if (movieName == FrontEndMovieService.FaceGenMovie)
            _characterCreation.OnFaceGenLoaded(layer, movie, dataSource);
    }

    public void Tick(float dt)
    {
        dt = FrameTime.Sanitize(dt);
        _mainMenu.Tick(dt);
        _characterCreation.Tick(dt);
        _factionScreen.Tick(dt);
    }
}
