using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TAOM.Core.Logging;
using TAOM.Features.BattleLoadDiagnostics;
using TAOM.Features.MissionPerf;
using TAOM.Features.TimeAcceleration;

namespace TAOM.Features.MapPerf.Hooks;

/// <summary>
/// Installs Patch101 at most once per process. Called on EVERY <c>OnGameInitializationFinished</c>, before
/// SubModule's once-per-process guard: the first call reads "Enable Map Profiler" and, off, logs one line and
/// applies nothing; on, it binds the listener walk, applies both categories, verifies each of the six core
/// targets is patched, creates the profiler only when the core category applied and nothing is missing, and
/// logs one install line (plus one warning per degraded part, and the not-installed warning when it failed).
/// It also hands <see cref="MapSessionHooks.LostHooks"/> the same patched check, so the session hooks can look
/// at the six targets again (PatchShield may strip them later). Later calls only report (restart needed, or the
/// install failed). Game init precedes every map frame, so no caller has run a target yet. Contains its own
/// failures. The targets and the call swap live in <see cref="MapProfilerTargets"/>.
/// </summary>
internal static class MapFrameProfilerInstaller
{
    internal const string Category = "Patch101_MapFrameProfiler";
    internal const string ViewsCategory = "Patch101_MapFrameProfiler_Views";

    private static bool _attempted;
    private static bool _offAtFirst;
    private static bool _installed;

    internal static void OnGameInitialized(IBattleLoadDiagnosticsSettingsProvider settings, ITimeControlAdapter timeControl,
        ITimeAccelerationSettingsProvider acceleration, IModLogger logger, Func<string, bool> tryPatchCategory) =>
        OnGameInitialized(settings, timeControl, acceleration, logger, tryPatchCategory, IsPatchedByThisProfiler);

    internal static void OnGameInitialized(IBattleLoadDiagnosticsSettingsProvider settings, ITimeControlAdapter timeControl,
        ITimeAccelerationSettingsProvider acceleration, IModLogger logger, Func<string, bool> tryPatchCategory, Func<MethodBase, bool> isPatched)
    {
        try
        {
            if (_attempted)
            {
                LaterGameInit(settings, logger);
                return;
            }
            _attempted = true;
            if (!settings.MapProfilerEnabled)
            {
                _offAtFirst = true;
                logger.LogInfo(MapProfileLines.OffLine);
                return;
            }

            MapFrameProfilerHooks.Logger = logger;
            (MapSessionHooks.Settings, MapSessionHooks.TimeControl, MapSessionHooks.Acceleration) = (settings, timeControl, acceleration);
            MapSessionHooks.LostHooks = () => MapProfilerTargets.UnpatchedCore(isPatched);   // looked at again at each session start, window start and measuring session end
            var walker = TickEventListenerWalker.TryBind(out var why);
            if (!walker)
                logger.LogWarning(MapProfileLines.BuildWalkerUnboundLine(why));

            var core = tryPatchCategory(Category);
            var views = tryPatchCategory(ViewsCategory);
            var missing = MapProfilerTargets.UnpatchedCore(isPatched);
            var viewTargets = MapProfilerTargets.MapViewTargets();
            var viewsPatched = viewTargets.Count(isPatched);
            _installed = core && missing.Count == 0;
            MapFrameProfilerHooks.Profiler = _installed ? new MapFrameProfiler(Stopwatch.Frequency) : null;
            var sites = MapFrameProfilerHooks.TickEventSites;
            var total = MapProfilerTargets.CoreTargetNames.Count;
            logger.LogInfo(MapProfileLines.BuildInstallLine(core, views, total - missing.Count, total,
                missing, viewsPatched, viewTargets.Count, sites, walker, AllocationCounter.Available));
            if (!_installed)
                logger.LogWarning(MapProfileLines.NotInstalledLine);
            if (_installed && sites != 1)
                logger.LogWarning(MapProfileLines.BuildTickEventVanillaLine(sites));
            if (_installed && viewsPatched < viewTargets.Count)
                logger.LogWarning(MapProfileLines.BuildViewsShortLine(viewsPatched, viewTargets.Count));
        }
        catch (Exception ex)
        {
            _installed = false;
            MapFrameProfilerHooks.Profiler = null;
            try { logger.LogError(MapProfileLines.BuildInstallFault(ex)); }
            catch { /* diagnostic only */ }
        }
    }

    // A later game init in the same process: nothing when installed or when the toggle reads off.
    private static void LaterGameInit(IBattleLoadDiagnosticsSettingsProvider settings, IModLogger logger)
    {
        if (_installed || !settings.MapProfilerEnabled)
            return;
        if (_offAtFirst)
            logger.LogInfo(MapProfileLines.RestartNeededLine);
        else
            logger.LogWarning(MapProfileLines.NotInstalledLine);
    }

    // Patched by one of this namespace's *_MapProfiler_Patch classes (a prefix, postfix or transpiler).
    internal static bool IsPatchedByThisProfiler(MethodBase method)
    {
        var info = Harmony.GetPatchInfo(method);
        return info != null && info.Prefixes.Concat(info.Postfixes).Concat(info.Transpilers).Any(p =>
            p.PatchMethod.DeclaringType is { } t && t.Namespace == typeof(MapFrameProfilerInstaller).Namespace
            && t.Name.EndsWith("_MapProfiler_Patch", StringComparison.Ordinal));
    }

    internal static void ResetForTests()
    {
        (_attempted, _offAtFirst, _installed) = (false, false, false);
        (MapFrameProfilerHooks.Profiler, MapFrameProfilerHooks.Logger, MapFrameProfilerHooks.TickEventSites) = (null, null, 0);
        MapSessionHooks.ResetForTests();
    }
}
