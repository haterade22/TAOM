using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.FactionUI.CharacterCreation;
using TAOM.Features.FactionUI.Menus;
using TAOM.Features.FactionUI.Presets;
using TAOM.Features.FactionUI.Resources;
using TAOM.Features.FactionUI.UI;

namespace TAOM.Features.FactionUI.Hooks;

/// <summary>
/// The services the Patch95_FactionUI entry points delegate to, set once from
/// <c>SubModule.OnSubModuleLoad</c> before the category is applied (#704). Null means not initialized:
/// every patch then leaves vanilla untouched.
/// </summary>
public static class FactionUIPatchContext
{
    internal const string Category = "Patch95_FactionUI";

    internal static FrontEndMovieService? Movies { get; private set; }

    internal static FrontEndSpriteService? Sprites { get; private set; }

    internal static MenuMediaService? Media { get; private set; }

    internal static LoadingImageService? LoadingImages { get; private set; }

    internal static IMenuMusicAdapter? Music { get; private set; }

    internal static IFrontEndStateAdapter? State { get; private set; }

    internal static FrontEndScreenEffects? Effects { get; private set; }

    internal static FaceGenCameraService? Camera { get; private set; }

    internal static FactionPresetService? Presets { get; private set; }

    internal static IModLogger? Logger { get; private set; }

    public static void Initialize(
        FrontEndMovieService movies,
        FrontEndSpriteService sprites,
        MenuMediaService media,
        LoadingImageService loadingImages,
        IMenuMusicAdapter music,
        IFrontEndStateAdapter state,
        FrontEndScreenEffects effects,
        FaceGenCameraService camera,
        FactionPresetService presets,
        IModLogger logger)
    {
        Movies = movies;
        Sprites = sprites;
        Media = media;
        LoadingImages = loadingImages;
        Music = music;
        State = state;
        Effects = effects;
        Camera = camera;
        Presets = presets;
        Logger = logger;
    }

    /// <summary>One warning per patch, so a failure that repeats every screen cannot flood the log.</summary>
    internal static void ReportOnce(ref bool reported, string patch, System.Exception ex)
    {
        if (reported)
            return;
        reported = true;
        Logger?.LogWarning($"[FactionUI] {patch} failed, vanilla behaviour kept: {ex.GetType().Name}: {ex.Message}");
    }
}
