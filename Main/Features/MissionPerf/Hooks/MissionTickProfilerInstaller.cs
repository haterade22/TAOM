using System;
using System.Collections.Generic;
using System.Diagnostics;
using HarmonyLib;
using TAOM.Core.Logging;
using TAOM.Features.BattleLoadDiagnostics;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.MissionPerf.Hooks;

/// <summary>
/// Installs Patch97 once per process, at TAOM's first <c>OnGameInitializationFinished</c>, and only when
/// "Enable Tick Profiler" is on there: game init precedes every mission, so nothing that calls
/// <c>Mission.OnPreTick</c> has run and no earlier-compiled caller holds an inlined copy that would bypass
/// the patch. Off, it applies no Patch97 transpiler and logs one line saying so. Either way it then hands over
/// to <see cref="HitchProbeInstaller"/>, which applies Patch98 (the frame boundary for both modes) when either
/// toggle is on. Its one caller, SubModule's once-per-process game-init block, sets its own guard before
/// calling, so a failing category is never retried (<c>MissionTickProfilerWiringTests</c> pins the placement).
/// Contains its own failures.
/// </summary>
internal static class MissionTickProfilerInstaller
{
    internal const string Category = "Patch97_MissionTickProfiler";

    /// <summary>Binds the private <c>Mission.WaitTickCompletion()</c> to an open delegate (lessons/harmony-il.md,
    /// "Call private engine methods from hot-path patches via a cached open delegate").</summary>
    internal static bool BindWaitDelegate()
    {
        try
        {
            var method = AccessTools.Method(typeof(Mission), "WaitTickCompletion", Type.EmptyTypes);
            MissionTickProfilerHooks.WaitTickCompletionCall = method == null
                ? null
                : (Action<Mission>)Delegate.CreateDelegate(typeof(Action<Mission>), method);
        }
        catch (Exception)
        {
            MissionTickProfilerHooks.WaitTickCompletionCall = null;
        }
        return MissionTickProfilerHooks.WaitTickCompletionCall != null;
    }

    internal static IReadOnlyList<CallSwap> OnTickSwaps() => new[]
    {
        Swap(typeof(MissionBehavior), nameof(MissionBehavior.OnPreDisplayMissionTick), nameof(MissionTickProfilerHooks.TimedPreDisplay)),
        Swap(typeof(MissionBehavior), nameof(MissionBehavior.OnMissionTick), nameof(MissionTickProfilerHooks.TimedMissionTick)),
    };

    internal static IReadOnlyList<CallSwap> OnPreTickSwaps()
    {
        var swaps = new List<CallSwap>(2);
        if (MissionTickProfilerHooks.WaitTickCompletionCall != null)
            swaps.Add(new CallSwap(
                AccessTools.Method(typeof(Mission), "WaitTickCompletion", Type.EmptyTypes),
                AccessTools.Method(typeof(MissionTickProfilerHooks), nameof(MissionTickProfilerHooks.TimedWaitTickCompletion))));
        swaps.Add(Swap(typeof(MissionBehavior), nameof(MissionBehavior.OnPreMissionTick), nameof(MissionTickProfilerHooks.TimedPreMissionTick)));
        return swaps;
    }

    internal static void InstallIfEnabled(IBattleLoadDiagnosticsSettingsProvider settings, IModLogger logger, Func<string, bool> tryPatchCategory)
    {
        var profilerOn = false;
        var applied = false;
        try
        {
            profilerOn = settings.TickProfilerEnabled;
            if (!profilerOn)
            {
                logger.LogInfo(TickProfileLines.OffLine);
            }
            else
            {
                MissionTickProfilerHooks.Logger = logger;
                MissionTickProfilerHooks.Profiler = new MissionTickProfiler(Stopwatch.Frequency);
                if (!BindWaitDelegate())
                    logger.LogWarning(TickProfileLines.WaitUnboundLine);
                var bound = MissionAttributionInstaller.BindScriptTickDelegate();
                if (!bound)
                    logger.LogWarning(HitchProbeLines.ScriptDelegateUnboundLine);

                applied = tryPatchCategory(Category);
                MissionTickProfilerHooks.Installed = applied && MissionTickProfilerHooks.OnTickSites == MissionTickProfilerHealth.ExpectedOnTickSites;
                logger.LogInfo(TickProfileLines.BuildInstallLine(applied, MissionTickProfilerHooks.OnTickSites,
                    MissionTickProfilerHooks.OnPreTickSites, MissionTickProfilerHooks.WaitTickCompletionCall != null ? 2 : 1,
                    AllocationCounter.Available));
                logger.LogInfo(HitchProbeLines.BuildAttributionInstallLine(MissionAttributionInstaller.SpawnSites,
                    MissionAttributionInstaller.ScriptSites, MissionAttributionInstaller.ScriptExpected, bound));
            }
        }
        catch (Exception ex)
        {
            MissionTickProfilerHooks.Installed = false;
            try { logger.LogError(TickProfileLines.BuildFault("install", ex)); }
            catch { /* diagnostic only */ }
        }

        // Patch98 owns the frame boundary for both modes; the wait swap is live exactly when Patch97 applied
        // with its delegate bound and both OnPreTick sites swapped (it does not need the OnTick sites).
        HitchProbeInstaller.Install(settings, logger, tryPatchCategory, profilerOn,
            applied && MissionTickProfilerHooks.WaitTickCompletionCall != null && MissionTickProfilerHooks.OnPreTickSites == 2);
    }

    private static CallSwap Swap(Type owner, string target, string helper) => new CallSwap(
        AccessTools.Method(owner, target, new[] { typeof(float) }),
        AccessTools.Method(typeof(MissionTickProfilerHooks), helper));
}
