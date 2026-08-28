using System;
using HarmonyLib;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.MountAndBlade.GauntletUI.BodyGenerator;
using TAOM.Core.Logging;

namespace TAOM.Features.PlayerSwitcher.Hooks;

/// <summary>
/// Tears the picker down with the view that hosts it.
/// </summary>
/// <remarks>
/// Deliberately does NOT clear the selection. CharacterCreationFaceGeneratorView.OnFinalize calls
/// this when the player leaves the stage in EITHER direction, and a selection has to survive
/// moving forward to the clan naming and review stages. Clearing happens on construction instead,
/// which covers going back and returning.
/// </remarks>
[HarmonyPatchCategory("Patch77_PlayerSwitcher")]
[HarmonyPatch(typeof(BodyGeneratorView), nameof(BodyGeneratorView.OnFinalize))]
public static class Patch77_BodyGeneratorView_OnFinalize
{
    private const string SpriteCategory = "ui_clan";

    private static IModLogger? _logger;

    public static void Initialize(IModLogger logger) => _logger = logger;

    [HarmonyPostfix]
    public static void Postfix(BodyGeneratorView __instance)
    {
        if (Patch77_BodyGeneratorView_Constructor.HostView != __instance)
            return;

        try
        {
            Release(__instance);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"Player Switcher: picker teardown was not clean: {ex.Message}");
        }
        finally
        {
            Patch77_BodyGeneratorView_Constructor.HostView = null;
            Patch77_BodyGeneratorView_Constructor.Movie = null;
            Patch77_BodyGeneratorView_Constructor.ViewModel = null;
            Patch77_BodyGeneratorView_Constructor.WeLoadedSpriteCategory = false;
        }
    }

    private static void Release(BodyGeneratorView view)
    {
        var movie = Patch77_BodyGeneratorView_Constructor.Movie;
        if (movie != null)
            view.GauntletLayer?.ReleaseMovie(movie);

        Patch77_BodyGeneratorView_Constructor.ViewModel?.OnFinalize();

        // Only unload what we loaded. SpriteCategory has no reference count, so unloading a sheet
        // another consumer is using would blank their icons with no error.
        if (Patch77_BodyGeneratorView_Constructor.WeLoadedSpriteCategory)
            UIResourceManager.GetSpriteCategory(SpriteCategory)?.Unload();
    }
}
