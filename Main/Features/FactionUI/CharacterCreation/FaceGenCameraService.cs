using System;
using System.Collections.Generic;
using TAOM.Features.FactionUI.Menus;

namespace TAOM.Features.FactionUI.CharacterCreation;

/// <summary>
/// Which themed character-creation screen owns the camera, so <c>BodyGeneratorView.InitCamera</c> can
/// apply that screen's <see cref="CameraOffsets"/> (#704, from Kysaro's <c>FaceGenCamera_Patch</c>).
/// The backstory, review and options views load their movie before they set up their camera (v1.5.3
/// CharacterCreationNarrativeStageView.cs:82 then :117, and likewise for the other two), so the screen
/// is known from the movie. The face generator sets up its camera in its constructor before loading its
/// movie (BodyGeneratorView.cs:156 then :185), so its screen is set from the constructor
/// (<see cref="OnFaceGeneratorOpening"/>). No camera is ever adjusted after its view has opened: Kysaro's
/// live re-adjust of a stored camera could reach a camera whose view had already closed.
/// </summary>
public sealed class FaceGenCameraService
{
    public const string FaceGenScreen = "FaceGen";
    public const string NarrativeScreen = "Narrative";
    public const string ReviewScreen = "Review";
    public const string OptionsScreen = "Options";

    private static readonly Dictionary<string, string> ScreenByMovie = new(StringComparer.Ordinal)
    {
        [FrontEndMovieService.NarrativeMovie] = NarrativeScreen,
        [FrontEndMovieService.ReviewMovie] = ReviewScreen,
        [FrontEndMovieService.OptionsMovie] = OptionsScreen,
        // Vanilla stage movies (a theme turned off) and the stages Kysaro did not tune: no offsets.
        ["CharacterCreationNarrativeStage"] = "",
        ["CharacterCreationReviewStage"] = "",
        ["CharacterCreationOptionsStage"] = "",
        ["CharacterCreationClanNamingStage"] = "",
        [FrontEndMovieService.ClanNamingMovie] = "",
        ["CharacterCreationCultureStage"] = "",
        [FrontEndMovieService.FactionScreenMovie] = "",
    };

    private readonly FaceGenCameraConfigProvider _config;

    public FaceGenCameraService(FaceGenCameraConfigProvider config)
    {
        _config = config;
    }

    public string CurrentScreen { get; private set; } = "";

    public CameraOffsets CurrentOffsets => CurrentScreen.Length == 0 ? CameraOffsets.None : _config.OffsetsFor(CurrentScreen);

    /// <summary>A face generator is being built: its offsets apply only when the themed one will load.</summary>
    public void OnFaceGeneratorOpening(bool themed) => CurrentScreen = themed ? FaceGenScreen : "";

    /// <summary>A movie finished loading; a character-creation stage movie decides the next camera.</summary>
    public void OnMovieLoaded(string? movieName)
    {
        if (movieName != null && ScreenByMovie.TryGetValue(movieName, out var screen))
            CurrentScreen = screen;
    }
}
