using System;
using System.Collections.Generic;
using TaleWorlds.Engine.GauntletUI;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.FactionMap.Hooks;
using TAOM.Features.FactionMap.Models;
using TAOM.Features.FactionMap.ViewModels;
using TAOM.Features.FactionUI.Menus;
using TAOM.Features.FactionUI.Presets;
using TAOM.Features.FactionUI.Resources;
using TAOM.Features.FactionUI.UI;
using TAOM.Features.FactionUI.UI.FactionScreen;
using TAOM.Features.PlayerSwitcher;

namespace TAOM.Features.FactionUI.FactionScreen;

/// <summary>
/// Puts Kysaro's faction screen on the culture stage in place of TAOM's faction map when the player has
/// it switched on (#704), through FactionMap's <see cref="ICultureStageMovieOverride"/> seam. In a
/// character creation where the screen is shown it replaces Player Switcher's picker panel (Mike's
/// decision: Kysaro's design), while Player Switcher's handover takes over a hero picked on the screen;
/// in one where it is switched off, Player Switcher's panel is offered as usual. Never throws: any
/// failure returns null and the faction map loads its own movie.
/// </summary>
public sealed class FactionScreenLauncher : ICultureStageMovieOverride
{
    private readonly FactionUISettingsProvider _settings;
    private readonly FactionScreenCatalog _catalog;
    private readonly FrontEndSpriteService _sprites;
    private readonly IFrontEndResourceAdapter _resources;
    private readonly FactionPickService _picks;
    private readonly FactionRoster _roster;
    private readonly FactionScreenWidgets _widgets;
    private readonly IPlayerSwitchPolicyProvider _playerSwitcher;
    private readonly IModLogger _logger;
    private FactionScreenVM? _screen;
    private bool _missingPrefabReported;
    private bool _failed;

    public FactionScreenLauncher(
        FactionUISettingsProvider settings,
        FactionScreenCatalog catalog,
        FrontEndSpriteService sprites,
        IFrontEndResourceAdapter resources,
        FactionPickService picks,
        FactionRoster roster,
        FactionScreenWidgets widgets,
        IPlayerSwitchPolicyProvider playerSwitcher,
        IModLogger logger)
    {
        _settings = settings;
        _catalog = catalog;
        _sprites = sprites;
        _resources = resources;
        _picks = picks;
        _roster = roster;
        _widgets = widgets;
        _playerSwitcher = playerSwitcher;
        _logger = logger;
    }

    public GauntletMovieIdentifier? TryLoad(
        GauntletLayer layer,
        FactionSelectionVM factionVm,
        IReadOnlyDictionary<string, RegionData> regions,
        IReadOnlyDictionary<string, FactionData> factions)
    {
        try
        {
            OnCultureStageClosed();
            // After one failure the screen is not built again until restart: a movie that threw while
            // building stays referenced by the engine's resource factories, so each retry would leak one.
            if (_failed || !_settings.Current.FactionScreen)
                return Decline();

            // Loaded by name, so a missing prefab would build an empty screen with nothing to confirm.
            if (!_resources.HasPrefab(FrontEndMovieService.FactionScreenMovie))
            {
                if (!_missingPrefabReported)
                {
                    _missingPrefabReported = true;
                    _logger.LogWarning($"[FactionUI] prefab {FrontEndMovieService.FactionScreenMovie} is not installed; showing TAOM's faction map");
                }
                return Decline();
            }

            var playable = _catalog.LoadPlayable(regions, factions);
            if (playable.Count == 0)
            {
                _logger.LogWarning("[FactionUI] no playable factions found; showing TAOM's faction map");
                return Decline();
            }

            // The screen opens on "Custom Character": a pick copied earlier in this creation goes back, and
            // a hero taken over earlier is no longer handed over.
            _picks.Clear();
            _widgets.Attach(layer);
            _screen = new FactionScreenVM(factionVm, playable, new FactionScreenServices(_sprites, _picks, _roster, _widgets));
            var movie = layer.LoadMovie(FrontEndMovieService.FactionScreenMovie, _screen);
            _playerSwitcher.SetPickerHidden(true);
            _logger.LogInfo($"[FactionUI] faction screen shown with {playable.Count} factions");
            return movie;
        }
        catch (Exception ex)
        {
            _failed = true;
            _logger.LogError($"[FactionUI] faction screen failed, showing TAOM's faction map until the game restarts: {ex.GetType().Name}: {ex.Message}");
            OnCultureStageClosed();
            return Decline();
        }
    }

    public void OnCultureStageClosed()
    {
        _widgets.Detach();
        try
        {
            _screen?.OnFinalize();
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[FactionUI] faction screen finalize failed: {ex.Message}");
        }
        _screen = null;
    }

    /// <summary>The game has ended, possibly straight from the faction screen ("Main Menu", or Esc, pops
    /// the character-creation state without finalizing the culture stage's view): lets go of the screen
    /// and any pick, and gives Player Switcher's panel back.</summary>
    public void ResetForGameEnd()
    {
        OnCultureStageClosed();
        _picks.ResetForNewCharacterCreation();
        _playerSwitcher.SetPickerHidden(false);
    }

    /// <summary>TAOM's faction map takes the stage: no faction-screen pick may stand (a pick copied in an
    /// earlier pass of this creation is put back), and Player Switcher's panel is offered as usual.</summary>
    private GauntletMovieIdentifier? Decline()
    {
        _picks.Clear();
        _playerSwitcher.SetPickerHidden(false);
        return null;
    }
}
