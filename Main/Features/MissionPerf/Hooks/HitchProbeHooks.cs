using System;
using System.Diagnostics;

namespace TAOM.Features.MissionPerf.Hooks;

/// <summary>
/// The Patch98 hitch probe's brackets (plan 041): a prefix and a finalizer around five whole engine methods
/// feed the open frame of <see cref="MissionTickProfilerHooks.Profiler"/>; thin (ADR-002). Each bracket reads
/// the profiler once and returns when there is none or it is not measuring; an open bracket is a <c>bool</c>,
/// never a 0 stamp. A fault turns that part off for the process with one ERROR line and never throws into the
/// engine. Pre-tick, wait and on-tick run on the main thread; spawn checks its thread (.Spawn.cs); script
/// runs from any thread (.Script.cs). Every enter hands its own call a <see cref="ProbeState"/> first and every
/// exit reads it, so a finalizer that runs without its prefix is told apart from one whose prefix returned early,
/// whichever thread or nesting level it runs on (.Diagnostics.cs).
/// </summary>
public static partial class HitchProbeHooks
{
    private const int WaitCheckFrames = 30;

    internal static AnimLoadingSampler? Sampler;

    /// <summary>Plan 028's live call-site swap times the wait, so this bracket records nothing (counted once).</summary>
    internal static bool WaitSwapActive;

    private static bool _preTickOff, _waitOff, _onTickOff;
    private static bool _preTickOpen, _waitOpen, _onTickOpen;
    private static long _preTickStart, _waitStart, _onTickStart;

    // Wait self-check: did the wait bracket run inside the first 30 measured pre-ticks (swap not live)?
    private static bool _waitSeen, _waitCheckDone;
    private static int _waitUnseenFrames;

    /// <summary>After the frame boundary: samples clip loading into the frame it opens, then opens the bracket.</summary>
    internal static void OnPreTickEnter(out ProbeState state)
    {
        state = ProbeState.Entered;
        var profiler = MissionTickProfilerHooks.Profiler;
        if (profiler == null || !profiler.Measuring || _preTickOff)
            return;
        try
        {
            _waitSeen = false;
            var sampler = Sampler;
            if (sampler != null && profiler.AnimSampling)
            {
                profiler.MarkAnimLoading(sampler.Sample());
                if (!sampler.Enabled)
                {
                    profiler.AnimSampling = false;
                    var fault = sampler.TakePendingFault();
                    if (fault != null)
                        MissionTickProfilerHooks.Logger?.LogWarning(fault);
                }
            }
            _preTickStart = Stopwatch.GetTimestamp();
            _preTickOpen = true;
        }
        catch (Exception ex) { _preTickOff = true; Sampler = null; profiler.AnimSampling = false; HookFault(HitchProbeLines.PreTickHookPart, ex); }
    }

    internal static void OnPreTickExit(ref ProbeState state)
    {
        if (!Pair(ref state, ProbeBracket.PreTick, HitchProbeLines.PreTickPrefixMissingLine) || !_preTickOpen)
            return;
        _preTickOpen = false;
        var profiler = MissionTickProfilerHooks.Profiler;
        if (profiler == null || !profiler.Measuring || _preTickOff)
            return;
        try
        {
            profiler.AddPreTickAll(Stopwatch.GetTimestamp() - _preTickStart);
            if (_waitCheckDone || WaitSwapActive)
                return;
            if (_waitSeen)
                _waitCheckDone = true;
            else if (++_waitUnseenFrames >= WaitCheckFrames)
            {
                _waitCheckDone = true;
                MissionTickProfilerHooks.Logger?.LogWarning(HitchProbeLines.WaitUnseenLine);
            }
        }
        catch (Exception ex) { _preTickOff = true; Sampler = null; profiler.AnimSampling = false; HookFault(HitchProbeLines.PreTickHookPart, ex); }
    }

    internal static void OnWaitEnter(out ProbeState state)
    {
        state = ProbeState.Entered;
        var profiler = MissionTickProfilerHooks.Profiler;
        if (profiler == null || !profiler.Measuring || _waitOff)
            return;
        try
        {
            _waitSeen = true;
            if (WaitSwapActive)
                return;
            _waitStart = Stopwatch.GetTimestamp();
            _waitOpen = true;
        }
        catch (Exception ex) { _waitOff = true; HookFault("wait", ex); }
    }

    internal static void OnWaitExit(ref ProbeState state)
    {
        if (!Pair(ref state, ProbeBracket.Wait, HitchProbeLines.WaitPrefixMissingLine) || !_waitOpen)
            return;
        _waitOpen = false;
        var profiler = MissionTickProfilerHooks.Profiler;
        if (profiler == null || !profiler.Measuring || _waitOff)
            return;
        try { profiler.AddWait(Stopwatch.GetTimestamp() - _waitStart); }
        catch (Exception ex) { _waitOff = true; HookFault("wait", ex); }
    }

    internal static void OnTickEnter(out ProbeState state)
    {
        state = ProbeState.Entered;
        var profiler = MissionTickProfilerHooks.Profiler;
        if (profiler == null || !profiler.Measuring || _onTickOff)
            return;
        try { _onTickStart = Stopwatch.GetTimestamp(); _onTickOpen = true; }
        catch (Exception ex) { _onTickOff = true; HookFault("on-tick", ex); }
    }

    internal static void OnTickExit(ref ProbeState state)
    {
        if (!Pair(ref state, ProbeBracket.OnTick, HitchProbeLines.OnTickPrefixMissingLine) || !_onTickOpen)
            return;
        _onTickOpen = false;
        var profiler = MissionTickProfilerHooks.Profiler;
        if (profiler == null || !profiler.Measuring || _onTickOff)
            return;
        try { profiler.AddOnTick(Stopwatch.GetTimestamp() - _onTickStart); }
        catch (Exception ex) { _onTickOff = true; HookFault("on-tick", ex); }
    }

    /// <summary>Test-only: every static back to its initial value, the once-per-process guards included.</summary>
    internal static void ResetForTests()
    {
        Sampler = null;
        WaitSwapActive = _waitSeen = _waitCheckDone = false;
        _preTickOff = _waitOff = _onTickOff = _preTickOpen = _waitOpen = _onTickOpen = false;
        _preTickStart = _waitStart = _onTickStart = 0;
        _waitUnseenFrames = 0;
        ResetDiagnosticsForTests();
        ResetSpawnForTests();
        ResetScriptForTests();
    }
}
