using System;
using HarmonyLib;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TAOM.Dependencies.Foundation;
using TAOM.Features.FactionUI.Menus;

namespace TAOM.Features.FactionUI.Hooks;

/// <summary>
/// Patch95_FactionUI: swaps a vanilla movie for Kysaro's themed one as it loads (#704), from his
/// <c>MainMenu_LoadMovie_Patch</c>. <see cref="FrontEndMovieService"/> decides, checks the themed prefab
/// is installed and registers the images first. If the themed movie throws while building, the
/// finalizer removes what the failed build attached to the layer and loads vanilla's movie instead, so
/// a broken prefab costs the theme, not the screen.
/// <para>
/// Target: the public <c>GauntletLayer.LoadMovie(string movieName, ViewModel dataSource)</c> (v1.5.3
/// GauntletLayer.cs:119), which every screen calls to load a movie; only the engine's resource refresh
/// reloads through the private overload (:126), and that reload keeps the themed name. The swap table
/// reads no setting until a movie has matched it.
/// </para>
/// </summary>
[HarmonyPatch(typeof(GauntletLayer), nameof(GauntletLayer.LoadMovie), new[] { typeof(string), typeof(ViewModel) })]
[HarmonyPatchCategory(FactionUIPatchContext.Category)]
public static class GauntletLayerLoadMoviePatch
{
    [ThreadStatic] private static bool _loadingVanillaFallback;
    private static bool _reported;

    static void Prefix(GauntletLayer __instance, ref string movieName, ViewModel dataSource, out SwapState? __state)
    {
        __state = null;
        var movies = FactionUIPatchContext.Movies;
        if (movies == null || _loadingVanillaFallback)
            return;

        try
        {
            var swap = movies.BeginLoad(movieName, dataSource?.GetType().Name);
            if (swap == null)
                return;
            __state = new SwapState(swap, __instance.UIContext?.Root?.ChildCount ?? 0);
            movieName = swap.TargetName;
        }
        catch (Exception ex)
        {
            __state = null;
            FactionUIPatchContext.ReportOnce(ref _reported, nameof(GauntletLayerLoadMoviePatch), ex);
        }
    }

    static void Postfix(GauntletLayer __instance, ViewModel dataSource, GauntletMovieIdentifier __result, SwapState? __state)
    {
        if (__result == null || _loadingVanillaFallback)
            return;

        try
        {
            if (__state != null)
                FactionUIPatchContext.Movies?.LoadSucceeded(__state.Swap, __result);
            FactionUIPatchContext.Effects?.OnMovieLoaded(__instance, __result, __result.MovieName, dataSource);
        }
        catch (Exception ex)
        {
            FactionUIPatchContext.ReportOnce(ref _reported, nameof(GauntletLayerLoadMoviePatch), ex);
        }
    }

    static Exception? Finalizer(
        Exception? __exception,
        SwapState? __state,
        GauntletLayer __instance,
        ViewModel dataSource,
        ref GauntletMovieIdentifier __result)
    {
        if (__exception == null)
            return null;
        if (__state == null)
            return RethrowStackPreserver.PreserveForRethrow(__exception, null);

        FactionUIPatchContext.Movies?.LoadFailed(__state.Swap);
        try
        {
            RemoveWhatTheFailedBuildAttached(__instance, __state.RootChildCount);
            // TAOM's own movie (the faction screen) has no vanilla movie to load instead; its launcher
            // catches the failure and shows the faction map.
            if (!__state.Swap.HasVanillaFallback)
                return RethrowStackPreserver.PreserveForRethrow(__exception, null);
            _loadingVanillaFallback = true;
            __result = __instance.LoadMovie(__state.Swap.OriginalName, dataSource);
        }
        catch
        {
            return RethrowStackPreserver.PreserveForRethrow(__exception, null);
        }
        finally
        {
            _loadingVanillaFallback = false;
        }

        try
        {
            FactionUIPatchContext.Effects?.OnMovieLoaded(__instance, __result, __result.MovieName, dataSource);
        }
        catch (Exception ex)
        {
            FactionUIPatchContext.ReportOnce(ref _reported, nameof(GauntletLayerLoadMoviePatch), ex);
        }
        return null;
    }

    // GauntletMovie's constructor attaches its root widget to the layer's root before the prefab is
    // instantiated (v1.5.3 GauntletMovie.cs:56-57), and a throw from the instantiation leaves it there,
    // unreleased, under the vanilla fallback.
    private static void RemoveWhatTheFailedBuildAttached(GauntletLayer layer, int childCountBefore)
    {
        var root = layer.UIContext?.Root;
        if (root == null)
            return;
        while (root.ChildCount > childCountBefore)
            root.RemoveChild(root.GetChild(root.ChildCount - 1));
    }

    /// <summary>The swap in progress and how many widgets the layer's root held before the load.</summary>
    internal sealed class SwapState
    {
        public SwapState(MovieSwap swap, int rootChildCount)
        {
            Swap = swap;
            RootChildCount = rootChildCount;
        }

        public MovieSwap Swap { get; }

        public int RootChildCount { get; }
    }
}
