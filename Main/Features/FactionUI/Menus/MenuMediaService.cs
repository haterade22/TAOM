using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;

namespace TAOM.Features.FactionUI.Menus;

/// <summary>
/// The main-menu video, the startup splash and the menu-music silence (#704), ported from Kysaro's
/// <c>MenuVideo_PlayVideo_Patch</c>, <c>SplashVideo_Patch</c> and <c>MenuMusic</c>.
/// <para>
/// Our menu videos live outside <c>Videos/initial_menu</c>, the folder vanilla picks its own at random
/// from (<c>MBInitialScreenBase.RefreshScene</c>, v1.5.3), so they play only through this service and
/// switching the option off removes them entirely.
/// </para>
/// <para>
/// The silence is a latch opened by <see cref="OnActivateMenuMode"/> and closed by <see cref="OnTick"/>
/// once the player has left the main menu. Per TAOM's latch rules the settings gate only the opening: a
/// silence already in force is always lifted on leaving, so turning the option off mid-menu cannot
/// leave the music stuck off.
/// </para>
/// </summary>
public sealed class MenuMediaService
{
    private const string MenuVideoMarker = "initial_menu";
    private const string SplashMarker = "TWLogo_and_Partners";

    private readonly IFrontEndResourceAdapter _adapter;
    private readonly FactionUIPaths _paths;
    private readonly FactionUISettingsProvider _settings;
    private readonly Random _random;

    private IReadOnlyList<(string Video, string Audio)>? _menuVideos;

    public MenuMediaService(IFrontEndResourceAdapter adapter, FactionUIPaths paths, FactionUISettingsProvider settings, Random random)
    {
        _adapter = adapter;
        _paths = paths;
        _settings = settings;
        _random = random;
    }

    public bool IsMenuMusicSilenced { get; private set; }

    /// <summary>A menu video of ours, with its audio, in place of the background video vanilla picked
    /// for the main menu.</summary>
    public (string Video, string Audio)? ChooseMenuVideo(string? requestedVideo)
    {
        if (requestedVideo == null
            || requestedVideo.IndexOf(MenuVideoMarker, StringComparison.OrdinalIgnoreCase) < 0
            || !_settings.Current.MenuVideo)
        {
            return null;
        }

        var videos = MenuVideos;
        return videos.Count == 0 ? null : videos[_random.Next(videos.Count)];
    }

    /// <summary>Our splash in place of the TaleWorlds-and-partners logo video. Not a setting: it plays
    /// before MCM has loaded the player's choices.</summary>
    public (string Video, string Audio)? ChooseSplash(string? requestedVideo)
    {
        if (requestedVideo == null || requestedVideo.IndexOf(SplashMarker, StringComparison.OrdinalIgnoreCase) < 0)
            return null;
        if (!_adapter.FileExists(_paths.SplashVideo) || !_adapter.FileExists(_paths.SplashAudio))
            return null;
        return (_paths.SplashVideo, _paths.SplashAudio);
    }

    /// <summary>True when vanilla's menu theme should not start: our video plays its own audio.</summary>
    public bool OnActivateMenuMode(bool isMainMenu)
    {
        if (!isMainMenu)
            return false;
        var settings = _settings.Current;
        if (!settings.MuteMenuMusic || !settings.MenuVideo || MenuVideos.Count == 0)
            return false;

        IsMenuMusicSilenced = true;
        return true;
    }

    /// <summary>True once, when the player has left the main menu while silenced: the caller sets the
    /// music back to paused, so vanilla starts its menu theme on the next music-menu state (the new-game
    /// loading screen) and, as in vanilla, stops it when character creation opens.</summary>
    public bool OnTick(bool isMainMenu)
    {
        if (!IsMenuMusicSilenced || isMainMenu)
            return false;
        IsMenuMusicSilenced = false;
        return true;
    }

    /// <summary>Every sub-folder of the menu videos holding a <c>*_pc.ivf</c> and an <c>.ogg</c>, listed
    /// once per process.</summary>
    private IReadOnlyList<(string Video, string Audio)> MenuVideos => _menuVideos ??= ListMenuVideos();

    private List<(string Video, string Audio)> ListMenuVideos()
    {
        var pairs = new List<(string Video, string Audio)>();
        foreach (var directory in _adapter.ListDirectories(_paths.MenuVideos))
        {
            var video = _adapter.ListFiles(directory, "_pc.ivf").FirstOrDefault();
            var audio = _adapter.ListFiles(directory, ".ogg").FirstOrDefault();
            if (video != null && audio != null)
                pairs.Add((video, audio));
        }
        return pairs;
    }
}
