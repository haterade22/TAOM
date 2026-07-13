using System;
using HarmonyLib;
using TaleWorlds.GauntletUI.Data;
using TAOM.Core.Logging;

namespace TAOM.Features.Arena.Hooks;

/// <summary>
/// Converts a heap-corruption <see cref="AccessViolationException"/> during
/// <c>GauntletMovie.Release()</c> from a CTD into a logged movie leak (issue #339).
///
/// WHY: a v2.0.12 player report (signature 4698b4d4) shows an AV inside
/// <c>WidgetFactory.IsCustomType</c> → <c>Dictionary.FindEntry</c> during the recursive
/// <c>WidgetTemplate.OnRelease</c> walk of the Tournament movie — fired first in Patch60's
/// early release (caught by its fail-safe), then again UNCAUGHT when the engine re-walked the
/// same corrupt tree releasing the leaked movie at ScreenManager.PopScreen. The corruption is
/// engine/native territory (a corrupt template-tree string; the prize tableau render was in
/// flight at exit, the #331 round-1 fingerprint); TAOM's fix is containment.
///
/// Suppressing here also fixes the double-walk: GauntletLayer.ReleaseMovie (Patch60's path)
/// calls this same method, then removes the movie from _movieIdentifiers — so the fatal
/// pop-time re-walk in ClearContext never sees the corrupt movie at all. The skipped tail of
/// Release() (global PrefabChange/BrushChange unhook, IsReleased flag) costs one bounded
/// leaked movie; GauntletLayer.OnFinalize's "not released" Debug.FailedAssert is a no-op in
/// shipping builds, and the leaked event subscriptions are inert there too — their producer
/// (ResourceDepot.CheckForChanges) only runs under _uiDebugMode. AVs are catchable in this
/// process — the game's launcher config sets legacyCorruptedStateExceptionsPolicy (how
/// Patch60's catch saw the first AV).
///
/// The Finalizer suppresses ONLY <see cref="AccessViolationException"/>; any other exception
/// propagates untouched. Mirrors the Patch50 vanilla-crash-guard pattern. Release() runs once
/// per movie lifetime — cold path, no per-frame cost.
/// </summary>
[HarmonyPatch(typeof(GauntletMovie), nameof(GauntletMovie.Release))]
[HarmonyPatchCategory("Patch62_MovieReleaseAvGuard")]
public static class GauntletMovie_Release_AvGuard_Patch
{
    private static IModLogger? _logger;

    public static void Initialize(IModLogger? logger) => _logger = logger;

    [HarmonyFinalizer]
    public static Exception? Finalizer(GauntletMovie? __instance, Exception? __exception)
    {
        if (__exception is not AccessViolationException) return __exception;

        string movieName = "<unknown>";
        try { movieName = __instance?.MovieName ?? "<unknown>"; }
        catch { /* corrupt instance — the name is diagnostic-only */ }

        _logger?.LogWarning(
            $"[Arena] Patch62 suppressed AccessViolationException releasing movie '{movieName}' — " +
            "movie leaked, game continues (issue #339, heap corruption in the widget-template tree)");
        return null;
    }
}
