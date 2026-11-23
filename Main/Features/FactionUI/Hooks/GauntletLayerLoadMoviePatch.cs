using System;
using System.Collections.Generic;
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
/// a broken prefab costs the theme, not the screen. When a co-op mod swallows that exception (#1545),
/// <see cref="GauntletMovieBuildWatchPatch"/> still marks the build as failed; for a swap with a vanilla movie,
/// the postfix takes the half-built movie off the layer and throws, so the finalizer falls back the same way.
/// <para>
/// Target: the public <c>GauntletLayer.LoadMovie(string movieName, ViewModel dataSource)</c> (v1.5.3
/// GauntletLayer.cs:119), which every screen calls to load a movie; only the engine's resource refresh
/// reloads through the private overload (:126), and that reload keeps the themed name. The swap table
/// reads no setting until a movie has matched it.
/// </para>
/// </summary>
[HarmonyPatch(typeof(GauntletLayer), nameof(GauntletLayer.LoadMovie), new[] { typeof(string), typeof(ViewModel) })]
[HarmonyPatchCategory(FactionUIPatchContext.Category)]
[ClientOnly]
public static class GauntletLayerLoadMoviePatch
{
    [ThreadStatic] private static bool _loadingVanillaFallback;
    private static bool _reported;
    private static bool _reportedDiscard;

    static void Prefix(GauntletLayer __instance, ref string movieName, ref ViewModel dataSource, out SwapState? __state)
    {
        __state = null;
        var movies = FactionUIPatchContext.Movies;
        if (movies == null || _loadingVanillaFallback)
            return;

        try
        {
            // Only a co-op mod TAOM cannot probe keeps the native map bar, and with it the vanilla Camp icon and army panel (ThemedMapBarCoopPolicy).
            if (ThemedMapBarCoopPolicy.IsMapBarMovie(movieName) && ThemedMapBarCoopPolicy.KeepsNativeMapBar(FactionUIPatchContext.Coop))
                return;

            var swap = movies.BeginLoad(movieName, dataSource?.GetType().Name);
            if (swap == null)
                return;
            var boundSource = FactionUIPatchContext.Presentation.Bind(swap.TargetName, dataSource);
            __state = new SwapState(swap, __instance.UIContext?.Root?.ChildCount ?? 0, dataSource);
            dataSource = (ViewModel)boundSource!;
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
        if (__state is { Swap.HasVanillaFallback: true } && GauntletMovieBuildWatchPatch.BuildThrew(__result.Movie))
        {
            Discard(__instance, __result);
            throw new InvalidOperationException($"{__result.MovieName} failed to build");
        }

        try
        {
            if (__state != null)
                FactionUIPatchContext.Movies?.LoadSucceeded(__state.Swap, __result);
            FactionUIPatchContext.Effects?.OnMovieLoaded(__instance, __result, __result.MovieName, dataSource);
            FactionUIPatchContext.Presentation.OnLoaded(__result, dataSource);
        }
        catch (Exception ex)
        {
            FactionUIPatchContext.ReportOnce(ref _reported, nameof(GauntletLayerLoadMoviePatch), ex);
        }
    }

    /// <summary>Takes a movie whose build threw under a co-op mod's finalizer (#1545) off the layer, so the
    /// throw that follows takes the same path as a build whose exception reached the layer. A half-built
    /// movie's <c>Release</c> can throw (a prefab that failed to parse leaves <c>_moviePrefab</c> null, v1.5.7
    /// GauntletMovie.cs:113) before the layer drops the identifier (GauntletLayer.cs:144-148); the layer's
    /// teardown would release it again and throw, so the identifier leaves the layer's list directly.</summary>
    private static void Discard(GauntletLayer layer, GauntletMovieIdentifier movie)
    {
        try
        {
            layer.ReleaseMovie(movie);
        }
        catch (Exception ex)
        {
            FactionUIPatchContext.ReportOnce(ref _reportedDiscard, nameof(GauntletLayerLoadMoviePatch), ex);
            (AccessTools.Field(typeof(GauntletLayer), "_movieIdentifiers")?.GetValue(layer) as List<GauntletMovieIdentifier>)?.Remove(movie);
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
            __result = __instance.LoadMovie(__state.Swap.OriginalName, __state.OriginalSource);
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
            FactionUIPatchContext.Presentation.OnLoaded(__result, dataSource);
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
        public SwapState(MovieSwap swap, int rootChildCount, ViewModel originalSource)
        {
            Swap = swap;
            RootChildCount = rootChildCount;
            OriginalSource = originalSource;
        }

        public MovieSwap Swap { get; }

        public int RootChildCount { get; }
        public ViewModel OriginalSource { get; }
    }
}
