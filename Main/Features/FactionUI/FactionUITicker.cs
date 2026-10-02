using System;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.FactionUI.Menus;
using TAOM.Features.FactionUI.Resources;
using TAOM.Features.FactionUI.UI;

namespace TAOM.Features.FactionUI;

/// <summary>
/// The front end's once-per-frame work (#704), called from <c>SubModule.OnApplicationTick</c>: releasing
/// the images of themed screens the engine has let go, lifting the menu-music silence once the player
/// has left the main menu (in place of Kysaro's prefix on the per-frame <c>MBMusicManager.Update</c>), and
/// every themed screen's animations. Each job is guarded on its own, so one failing cannot stop the
/// others, and each reports its first failure once.
/// <para>
/// Kysaro's ticker also removed an overlay layer belonging to an assembly named
/// <c>TAOM.CharacterDisplay</c>, through its private statics. That mod is not part of TAOM or its
/// install, so the suppressor is not ported; what it is was asked of Kysaro.
/// </para>
/// </summary>
public sealed class FactionUITicker
{
    private readonly FrontEndSpriteService _sprites;
    private readonly FrontEndMovieService _movies;
    private readonly MenuMediaService _media;
    private readonly IMenuMusicAdapter _music;
    private readonly IFrontEndStateAdapter _state;
    private readonly FrontEndScreenEffects _effects;
    private readonly IModLogger _logger;
    private bool _imagesReported;
    private bool _musicReported;
    private bool _effectsReported;

    public FactionUITicker(
        FrontEndSpriteService sprites,
        FrontEndMovieService movies,
        MenuMediaService media,
        IMenuMusicAdapter music,
        IFrontEndStateAdapter state,
        FrontEndScreenEffects effects,
        IModLogger logger)
    {
        _sprites = sprites;
        _movies = movies;
        _media = media;
        _music = music;
        _state = state;
        _effects = effects;
        _logger = logger;
    }

    public void Tick(float dt)
    {
        try
        {
            _sprites.Tick(_movies.Tick());
        }
        catch (Exception ex)
        {
            Report(ref _imagesReported, "image release", ex);
        }

        try
        {
            if (_media.IsMenuMusicSilenced && _media.OnTick(_state.IsMainMenuActive()))
                _music.MarkPaused(null);
        }
        catch (Exception ex)
        {
            Report(ref _musicReported, "menu music", ex);
        }

        try
        {
            _effects.Tick(dt);
        }
        catch (Exception ex)
        {
            Report(ref _effectsReported, "screen effects", ex);
        }
    }

    private void Report(ref bool reported, string job, Exception ex)
    {
        if (reported)
            return;
        reported = true;
        _logger.LogWarning($"[FactionUI] front-end tick failed ({job}): {ex.GetType().Name}: {ex.Message}");
    }
}
