using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.FactionUI.Resources;

namespace TAOM.Features.FactionUI.Menus;

/// <summary>
/// Decides which vanilla movies get Kysaro's themed version (#704) and ties each themed screen's
/// lifetime to the images it draws. Ported from the movie table in his <c>MainMenu_LoadMovie_Patch</c>;
/// a swap is chosen by movie name AND view-model type, so another mod's movie that happens to share a
/// name is left alone, and only when the themed prefab is installed. The face generator and the banner
/// editor also open in a running campaign (the barber, the clan screen); Kysaro themed those too,
/// untested, so they are themed here only inside character creation.
/// <para>
/// A themed movie holds its image groups for as long as the engine keeps it. The engine releases a
/// screen's movies through the layer's own finalize far more often than through
/// <c>GauntletLayer.ReleaseMovie</c> (v1.5.3: the main menu and every character-creation stage are torn
/// down by <c>RemoveLayer</c> or a screen pop, which calls <c>Movie.Release()</c> directly), so
/// <see cref="Tick"/> asks each held movie whether it has been released, whichever way that happened.
/// </para>
/// </summary>
public sealed class FrontEndMovieService
{
    public const string MainMenuMovie = "TAOMMainMenu";
    public const string EmptyMovie = "TAOMEmpty";
    public const string LoadingWindowMovie = "TAOMLoadingWindow";
    public const string FaceGenMovie = "TAOMFaceGen";
    public const string NarrativeMovie = "TAOMNarrativeStage";
    public const string ReviewMovie = "TAOMReviewStage";
    public const string BannerEditorMovie = "TAOMBannerEditor";
    public const string ClanNamingMovie = "TAOMClanNaming";
    public const string OptionsMovie = "TAOMOptionsStage";
    public const string FactionScreenMovie = "TAOMFactionScreen";

    private const FrontEndImageGroups CreationFrame = FrontEndImageGroups.CharacterCreation;

    private readonly FrontEndSpriteService _sprites;
    private readonly FactionUISettingsProvider _settings;
    private readonly IFrontEndStateAdapter _state;
    private readonly IFrontEndResourceAdapter _resources;
    private readonly IModLogger _logger;
    private readonly List<(object Movie, FrontEndImageGroups Groups)> _held = new();
    private readonly HashSet<string> _missingPrefabsReported = new();
    private readonly HashSet<string> _failedBuilds = new();

    public FrontEndMovieService(
        FrontEndSpriteService sprites,
        FactionUISettingsProvider settings,
        IFrontEndStateAdapter state,
        IFrontEndResourceAdapter resources,
        IModLogger logger)
    {
        _sprites = sprites;
        _settings = settings;
        _state = state;
        _resources = resources;
        _logger = logger;
    }

    private FactionUISettings Settings => _settings.Current;

    /// <summary>The themed movie to load in place of <paramref name="movieName"/>, with its images
    /// registered first, or null to load vanilla's. Runs for every movie the game loads, so the
    /// settings are read only once a movie has matched.</summary>
    public MovieSwap? BeginLoad(string? movieName, string? dataSourceTypeName)
    {
        switch (movieName)
        {
            case "InitialScreen" when dataSourceTypeName == "InitialMenuVM":
                var settings = Settings;
                if (settings.SkillIcons)
                    _sprites.ApplySkillIcons();
                return settings.CustomMainMenu ? Themed(movieName, MainMenuMovie, FrontEndImageGroups.MainMenu) : null;

            case "GameVersion" when dataSourceTypeName == "GameVersionVM":
                return Settings.HideGameVersion ? Themed(movieName, EmptyMovie, FrontEndImageGroups.None) : null;

            // Built once, on the engine's first application tick, and kept for the process
            // (GauntletUISubModule.OnApplicationTick, v1.5.3): always themed, since MCM has not loaded
            // the player's settings that early.
            case "LoadingWindow" when dataSourceTypeName == "LoadingWindowViewModel":
                if (!CanTheme(LoadingWindowMovie))
                    return null;
                _sprites.EnsureResident();
                return new MovieSwap(movieName, LoadingWindowMovie, FrontEndImageGroups.None);

            case "FaceGen" when dataSourceTypeName == "FaceGenVM":
                return WillThemeFaceGenerator() ? Themed(movieName, FaceGenMovie, CreationFrame) : null;

            case "CharacterCreationNarrativeStage" when dataSourceTypeName == "CharacterCreationNarrativeStageVM":
                return Settings.CustomNarrativeStage ? Themed(movieName, NarrativeMovie, CreationFrame) : null;

            case "CharacterCreationReviewStage" when dataSourceTypeName == "CharacterCreationReviewStageVM":
                return Settings.CustomReviewStage ? Themed(movieName, ReviewMovie, CreationFrame) : null;

            case "BannerEditor" when dataSourceTypeName == "BannerEditorVM":
                return Settings.CustomBannerEditor && _state.IsInCharacterCreation() ? Themed(movieName, BannerEditorMovie, CreationFrame) : null;

            case "CharacterCreationClanNamingStage" when dataSourceTypeName == "CharacterCreationClanNamingStageVM":
                return Settings.CustomClanNaming ? Themed(movieName, ClanNamingMovie, CreationFrame) : null;

            case "CharacterCreationOptionsStage" when dataSourceTypeName == "CharacterCreationOptionsStageVM":
                return Settings.CustomOptionsStage ? Themed(movieName, OptionsMovie, CreationFrame) : null;

            // Loaded by name from the culture stage hook once the faction screen is chosen; the swap
            // here only takes the image hold for it.
            case FactionScreenMovie when dataSourceTypeName == "FactionScreenVM":
                return Themed(movieName, FactionScreenMovie, CreationFrame | FrontEndImageGroups.FactionArt);

            default:
                return null;
        }
    }

    /// <summary>Whether the face generator about to be built gets the themed movie. Its camera offsets
    /// follow the same decision.</summary>
    public bool WillThemeFaceGenerator() => Settings.CustomFaceGen && _state.IsInCharacterCreation();

    /// <summary>The themed movie is built; <paramref name="movie"/> is the engine's handle to it.</summary>
    public void LoadSucceeded(MovieSwap swap, object movie)
    {
        if (swap.Holds != FrontEndImageGroups.None)
            _held.Add((movie, swap.Holds));
    }

    /// <summary>The themed movie threw while building. Its groups are simply never held, so they are
    /// released once nothing else holds them. It is not tried again until the game restarts: the engine
    /// keeps a failed movie subscribed to its resource factories (v1.5.3 <c>GauntletMovie.cs:52-53</c>,
    /// undone only by <c>Release</c>, which nothing can call on it), so every retry would leak another.</summary>
    public void LoadFailed(MovieSwap swap)
    {
        _failedBuilds.Add(swap.TargetName);
        _logger.LogWarning($"[FactionUI] {swap.TargetName} failed to build; not used again until the game restarts");
    }

    /// <summary>Once per application tick: forgets every held movie the engine has released and returns
    /// the image groups the live ones hold.</summary>
    public FrontEndImageGroups Tick()
    {
        var held = FrontEndImageGroups.None;
        for (var i = _held.Count - 1; i >= 0; i--)
        {
            if (_state.IsMovieReleased(_held[i].Movie))
            {
                _held.RemoveAt(i);
                continue;
            }
            held |= _held[i].Groups;
        }
        return held;
    }

    private MovieSwap? Themed(string originalName, string targetName, FrontEndImageGroups groups)
    {
        if (!CanTheme(targetName))
            return null;
        _sprites.Acquire(groups);
        return new MovieSwap(originalName, targetName, groups);
    }

    // A swap to a prefab that is not installed would load nothing and leave the screen blank
    // (GauntletMovie.LoadMovie builds nothing when WidgetFactory.GetCustomType returns null, v1.5.3),
    // so the vanilla movie is kept instead; so it is after the prefab has failed to build once.
    private bool CanTheme(string targetName)
    {
        if (_failedBuilds.Contains(targetName))
            return false;
        if (_resources.HasPrefab(targetName))
            return true;
        if (_missingPrefabsReported.Add(targetName))
            _logger.LogWarning($"[FactionUI] prefab {targetName} is not installed; the vanilla screen is kept");
        return false;
    }
}
