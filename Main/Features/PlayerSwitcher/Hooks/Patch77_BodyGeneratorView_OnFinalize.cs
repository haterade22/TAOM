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

    /// <summary>
    /// A FINALIZER, deliberately, not a postfix.
    ///
    /// A postfix does not run when the original method throws, and this body is the only thing
    /// that releases the movie, disposes the ViewModel graph and drops the sprite category. If
    /// vanilla's own OnFinalize threw partway (it tears down a scene and an agent renderer), a
    /// postfix would silently skip all of that and leave the statics pointing at a dead view.
    /// A finalizer runs either way.
    ///
    /// It returns <paramref name="__exception"/> unchanged, so a vanilla failure still propagates.
    /// Swallowing it here would convert an engine bug into a silent leak somewhere else.
    /// </summary>
    [HarmonyFinalizer]
    public static Exception? Finalizer(BodyGeneratorView __instance, Exception? __exception)
    {
        if (Patch77_BodyGeneratorView_Constructor.HostView != __instance)
            return __exception;

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

        return __exception;
    }

    private static void Release(BodyGeneratorView view)
    {
        var movie = Patch77_BodyGeneratorView_Constructor.Movie;
        if (movie != null)
            view.GauntletLayer?.ReleaseMovie(movie);

        Patch77_BodyGeneratorView_Constructor.ViewModel?.OnFinalize();

        // ui_clan is deliberately NOT unloaded, even when this feature was the one that loaded it.
        //
        // SpriteCategory carries a bare IsLoaded bool and no reference count, so "I loaded it" is
        // not ownership. If any other screen or mod starts using the category while the picker is
        // open, its own Load() is a no-op, and unloading here would release the textures out from
        // under it with no error and no log line. Leaving a vanilla category resident costs some
        // memory; releasing a sheet somebody else is drawing from costs them their icons.
    }
}
