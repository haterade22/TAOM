using System;
using System.Collections.Generic;
using TAOM.Features.BattleLoadDiagnostics;
using TAOM.Features.MissionPerf;
using TAOM.Features.TimeAcceleration;

namespace TAOM.Features.MapPerf.Hooks;

/// <summary>
/// The campaign session behind the Patch101 frame boundary (<see cref="MapFrameBoundary"/>). A session is one
/// <c>Campaign</c>, held only through a <see cref="WeakReference"/>; another campaign closes it (<c>newCampaign</c>,
/// else <c>gameEnd</c> from <c>SubModule.OnGameEnd</c>) and opens the next with fresh settings and accumulators. A
/// <c>[MapProfile]</c> line every 5 s of wall clock, checked at boundaries. A fault logs one error, writes the frames
/// already closed as a <c>reason=fault</c> summary and stops measuring the current campaign; that campaign becomes the
/// session, so it is not measured or retried again. A fault in writing a summary (session end, or closing the
/// previous campaign) loses that summary: the retry runs the same code over the same state. At a session start, at
/// each window start and before the summary of a measuring session <see cref="LostHooks"/> says whether PatchShield
/// stripped a core hook since: if so one warning, the frames closed as a <c>reason=hooksLost</c> summary, and measuring
/// stops. A strip on <c>MapState.OnTick</c> removes the boundary that runs the window check, so only a session end
/// finds that one. Main thread only.
/// </summary>
public static class MapSessionHooks
{
    private const int DefaultTopN = 8, DefaultFastForward = 4, DefaultExtraFastForward = 8;

    internal static IBattleLoadDiagnosticsSettingsProvider? Settings;
    internal static ITimeControlAdapter? TimeControl;
    internal static ITimeAccelerationSettingsProvider? Acceleration;
    internal static Func<IReadOnlyList<string>>? LostHooks;   // set by the installer: the required hooks no longer patched
    internal static Func<int> PartyCount = MapFrameBoundary.ReadPartyCount;   // a cached method group: no per-call allocation
    internal static Func<int?> RawTopN = MapFrameBoundary.ReadRawTopN;

    private static WeakReference? _session;
    private static int _topN = DefaultTopN, _fastForward = DefaultFastForward, _extraFastForward = DefaultExtraFastForward;

    // On but not measuring (toggle off, or a faulted session) on this session's campaign: a flag and a reference
    // compare, before the boundary's engine reads. A new campaign is never settled, so Step opens its session.
    internal static bool IsSettled(MapFrameProfiler profiler, object campaign) =>
        !profiler.Measuring && ReferenceEquals(_session?.Target, campaign);

    internal static void Step(object campaign, long nowTicks, long allocBytesNow, MapSkip skip)
    {
        var profiler = MapFrameProfilerHooks.Profiler;
        if (profiler == null) return;
        try
        {
            if (!ReferenceEquals(_session?.Target, campaign))
            {
                WriteSummary(profiler, WarnIfHooksLost(profiler) ? "hooksLost" : "newCampaign");
                OpenSession(profiler, campaign, nowTicks);
            }
            if (!profiler.Measuring) return;
            profiler.Boundary(nowTicks, allocBytesNow, skip, ReadSpeed());
            if (!profiler.WindowDue(nowTicks)) return;
            ReadMultipliers();
            var window = profiler.TakeWindow(nowTicks, _topN, GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2));
            MapFrameProfilerHooks.Logger?.LogInfo(MapProfileLines.BuildMapProfile(
                profiler.SecondsSinceSessionStart(nowTicks), window, PartyCount(), AllocationCounter.Available));
            // The next window starts here: look at the core hooks again. The line above stays, it holds what was measured.
            if (WarnIfHooksLost(profiler)) WriteSummary(profiler, "hooksLost");
        }
        catch (Exception ex) { Fault(profiler, "frame boundary", ex, campaign); }
    }

    /// <summary>Closes the open session; a no-op when the profiler is not installed or no session is open.</summary>
    public static void EndSession(string reason)
    {
        var profiler = MapFrameProfilerHooks.Profiler;
        if (profiler == null || _session == null) return;
        try { _session = null; WriteSummary(profiler, WarnIfHooksLost(profiler) ? "hooksLost" : reason); }
        catch (Exception ex) { Fault(profiler, "session end", ex, null); }
    }

    // One look at the core hooks (a patch-info read each, so only where a session or a window starts or ends): when
    // PatchShield stripped one since the last look, the one warning for this session. A stopped session has none to lose.
    private static bool WarnIfHooksLost(MapFrameProfiler profiler)
    {
        if (!profiler.Measuring || LostHooks?.Invoke() is not { Count: > 0 } lost) return false;
        MapFrameProfilerHooks.Logger?.LogWarning(MapProfileLines.BuildHooksLostLine(profiler.Session, lost));
        return true;
    }

    private static void OpenSession(MapFrameProfiler profiler, object campaign, long nowTicks)
    {
        _topN = Settings?.TickProfilerTopN ?? DefaultTopN;
        ReadMultipliers();
        var wanted = Settings?.MapProfilerEnabled ?? false;
        var lost = wanted ? LostHooks?.Invoke() : null;   // stripped since the install (or the last session)?
        var measuring = wanted && (lost == null || lost.Count == 0);
        var session = profiler.BeginSession(nowTicks, measuring, GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2));
        _session = new WeakReference(campaign);
        var logger = MapFrameProfilerHooks.Logger;
        if (wanted && !measuring)
            logger?.LogWarning(MapProfileLines.BuildHooksLostLine(session, lost!));
        else
            logger?.LogInfo(measuring ? MapProfileLines.BuildSessionStartLine(session, _topN, _fastForward, _extraFastForward)
                : MapProfileLines.BuildSessionOffLine(session));
        var raw = measuring ? RawTopN() : null;
        if (raw.HasValue && raw.Value != _topN)
            logger?.LogWarning(MapProfileLines.BuildTopNFallbackLine(raw.Value, _topN));
    }

    // Ends a measuring session: [MapProfileSummary] when a frame closed, else the no-frames line; then stops measuring.
    private static void WriteSummary(MapFrameProfiler profiler, string reason)
    {
        if (!profiler.Measuring) return;
        var summary = profiler.Summarize(_topN);
        MapFrameProfilerHooks.Logger?.LogInfo(summary.Frames > 0
            ? MapProfileLines.BuildSummary(reason, summary, AllocationCounter.Available)
            : MapProfileLines.BuildNoFramesLine(profiler.Session, reason));
        profiler.StopMeasuring();
    }

    // The frame's speed class, read after OpenSession so a new campaign's multipliers apply: from the engine's simplified
    // mode, so a Stoppable mode with the main party waiting (no campaign time) reads Stop (FOR-MIKE 16r).
    private static MapSpeedClass ReadSpeed()
    {
        var time = TimeControl;
        return MapSpeed.Classify(time?.SimplifiedTimeControlMode ?? -1, time?.SpeedUpMultiplier ?? float.NaN, _fastForward, _extraFastForward);
    }

    private static void ReadMultipliers() => (_fastForward, _extraFastForward) =
        (Acceleration?.FastForwardMultiplier ?? DefaultFastForward, Acceleration?.ExtraFastForwardMultiplier ?? DefaultExtraFastForward);

    // One error line, then the frames already closed as a reason=fault summary (D6), then stop measuring. When
    // the fault was in writing a summary, the retry throws again and is swallowed, so that summary is lost. The
    // campaign becomes the session, so its later boundaries return at IsSettled: no retry and no repeat line.
    internal static void Fault(MapFrameProfiler profiler, string where, Exception ex, object? campaign)
    {
        try
        {
            if (campaign != null) _session = new WeakReference(campaign);
            MapFrameProfilerHooks.Logger?.LogError(MapProfileLines.BuildFault(where, ex));
            WriteSummary(profiler, "fault");
        }
        catch { /* diagnostic only: never throw out of a map frame */ }
        finally { profiler.StopMeasuring(); }
    }

    internal static void ResetForTests()
    {
        _session = null;
        (_topN, _fastForward, _extraFastForward) = (DefaultTopN, DefaultFastForward, DefaultExtraFastForward);
        (Settings, TimeControl, Acceleration, LostHooks) = (null, null, null, null);
        PartyCount = MapFrameBoundary.ReadPartyCount;
        RawTopN = MapFrameBoundary.ReadRawTopN;
    }
}
