using System.Runtime.CompilerServices;
using HarmonyLib;
using TaleWorlds.GauntletUI.Data;
using TAOM.Dependencies.Foundation;

namespace TAOM.Features.FactionUI.Hooks;

/// <summary>
/// Patch95_FactionUI: marks each movie while the engine builds it, so <see cref="GauntletLayerLoadMoviePatch"/>
/// can tell a themed build that threw even when another mod swallowed the exception (#1545). BannerlordCoop's
/// <c>RobustnessPatches</c> finalizes this same method and returns null; Harmony skips a postfix whenever the
/// original throws, finalizer or not (<c>HarmonyPatchSemanticsTests</c>), so a mark that the postfix never
/// cleared means the build failed: while parsing the prefab, instantiating it or binding it.
/// <para>
/// Target: the private <c>GauntletMovie.LoadMovie()</c> (v1.5.7 GauntletMovie.cs:91-107), which
/// <c>GauntletMovie.Load</c> calls right after the constructor for every non-generated movie (:165-166); hot
/// reload (<c>RefreshResources</c>, :80) calls it again on the same movie and re-marks it.
/// </para>
/// </summary>
[HarmonyPatch(typeof(GauntletMovie), "LoadMovie")]
[HarmonyPatchCategory(FactionUIPatchContext.Category)]
[ClientOnly]
public static class GauntletMovieBuildWatchPatch
{
    private static readonly ConditionalWeakTable<GauntletMovie, object> Building = new();
    private static readonly object Mark = new();

    static void Prefix(GauntletMovie __instance)
    {
        Building.Remove(__instance);
        Building.Add(__instance, Mark);
    }

    static void Postfix(GauntletMovie __instance) => Building.Remove(__instance);

    /// <summary>True when the engine's build of <paramref name="movie"/> threw and the exception never
    /// reached the caller.</summary>
    internal static bool BuildThrew(IGauntletMovie? movie) =>
        movie is GauntletMovie built && Building.TryGetValue(built, out _);
}
