using System;
using System.Reflection;
using HarmonyLib;
using SandBox.GauntletUI.Missions;
using TaleWorlds.Engine.GauntletUI;
using TAOM.Core.Logging;

namespace TAOM.Features.Arena.Hooks;

// Issue #331 — tournament exits hang the loading screen ~30s-2min (measured 108s) while every
// other mission exit is instant.
//
// Root cause (engine defect, verified against installed 1.4.6): MissionGauntletTournamentView.
// OnMissionScreenFinalize nulls _gauntletMovie/_gauntletLayer WITHOUT ReleaseMovie/RemoveLayer
// (the arena practice view releases both properly at the same hook). The leaked 'Tournament'
// movie — the only mission UI holding live item-tableau/character-tableau widgets (prize item,
// per-round weapon icons, winner panel), with a prize render request typically still in flight
// at exit — is then torn down inside ScreenBase.HandleFinalize's layer loop, after the mission
// frame pump is dead under the exit loading screen, where that teardown stalls ~108s instead of
// the milliseconds it costs while the mission is alive.
//
// Fix: replicate the practice view's release sequence at the identical lifecycle point
// (IMissionListener.OnEndMission, mission renderer still alive). The original body must run
// first — it drops focus and finalizes the VM, and it nulls the private fields — so a Prefix
// captures the layer/movie into __state and a Postfix releases them. Worst case on any failure
// is today's vanilla leak (fail-safe). Reflection (not FieldRefAccess) keeps the drift-guard
// test surface free of module types, per the Patch58 MethodInfo precedent; this path runs once
// per tournament exit, so per-call GetValue cost is irrelevant.
[HarmonyPatch(typeof(MissionGauntletTournamentView), nameof(MissionGauntletTournamentView.OnMissionScreenFinalize))]
[HarmonyPatchCategory("Patch60_TournamentExitMovieRelease")]
public static class Patch60_TournamentExitMovieRelease
{
    // internal (not private) so Patch60TournamentExitMovieReleaseTests can drift-guard the
    // bindings against the installed engine — a field rename silently disables the fix.
    internal static readonly FieldInfo? LayerField =
        AccessTools.Field(typeof(MissionGauntletTournamentView), "_gauntletLayer");
    internal static readonly FieldInfo? MovieField =
        AccessTools.Field(typeof(MissionGauntletTournamentView), "_gauntletMovie");

    private static IModLogger? _logger;

    public static void Initialize(IModLogger logger) => _logger = logger;

    [HarmonyPrefix]
    public static void Prefix(
        MissionGauntletTournamentView __instance,
        out ValueTuple<GauntletLayer?, GauntletMovieIdentifier?> __state)
    {
        __state = default;
        try
        {
            if (LayerField == null || MovieField == null) return;
            __state = new ValueTuple<GauntletLayer?, GauntletMovieIdentifier?>(
                LayerField.GetValue(__instance) as GauntletLayer,
                MovieField.GetValue(__instance) as GauntletMovieIdentifier);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"[Arena] Patch60 capture failed — tournament UI will leak (vanilla behavior): {ex.GetType().Name}: {ex.Message}");
        }
    }

    [HarmonyPostfix]
    public static void Postfix(
        MissionGauntletTournamentView __instance,
        ValueTuple<GauntletLayer?, GauntletMovieIdentifier?> __state)
    {
        var (layer, movie) = __state;
        if (layer == null || movie == null) return;
        try
        {
            // Practice-view parity: the original body already finalized the VM and dropped
            // focus; ReleaseMovie is idempotence-guarded (Contains + IsReleased) and RemoveLayer
            // finalizes the layer now, while the mission renderer still services tableau work.
            // Timed per call (#331 round 2): the ~107s exit stall moved WITH this release, so
            // these stamps split the cost between ReleaseMovie and RemoveLayer for the RCA.
            var sw = System.Diagnostics.Stopwatch.StartNew();
            layer.ReleaseMovie(movie);
            long releaseMs = sw.ElapsedMilliseconds;

            var screen = __instance.MissionScreen;
            if (screen != null && screen.HasLayer(layer))
                screen.RemoveLayer(layer);

            _logger?.LogInfo($"[Arena] Patch60 tournament UI released: ReleaseMovie={releaseMs}ms RemoveLayer={sw.ElapsedMilliseconds - releaseMs}ms");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"[Arena] Patch60 release failed — tournament UI leaked this exit (vanilla behavior): {ex.GetType().Name}: {ex.Message}");
        }
    }
}
