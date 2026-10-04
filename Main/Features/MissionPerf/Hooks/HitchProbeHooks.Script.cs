using System;
using System.Diagnostics;
using System.Threading;

namespace TAOM.Features.MissionPerf.Hooks;

/// <summary>
/// The <c>ManagedScriptHolder.TickComponents</c> bracket. Native calls it for every scene with script
/// components, from a thread the repo has not recorded, so the open flag and start stamp are
/// <c>[ThreadStatic]</c> and the time goes into the open frame through <c>Interlocked</c>. The first measured
/// call of the process names its thread once (INFO on the main thread, WARNING elsewhere); off the main thread,
/// <see cref="ScriptOffMain"/> turns per-component attribution off for the process and the totals stay. A
/// finalizer whose own call had no prefix (see <see cref="HitchProbeHooks"/>) records nothing, whichever thread it
/// runs on; it is counted in the missing-prefix total, and the first one per process writes the missing-prefix
/// WARNING line.
/// </summary>
public static partial class HitchProbeHooks
{
    /// <summary>True once the first measured script tick ran off the main thread.</summary>
    internal static volatile bool ScriptOffMain;

    private static volatile bool _scriptOff;
    private static int _scriptThreadLogged;

    [ThreadStatic] private static bool _scriptOpen;
    [ThreadStatic] private static long _scriptStart;

    internal static void OnScriptTickEnter(out ProbeState state)
    {
        state = ProbeState.Entered;
        var profiler = MissionTickProfilerHooks.Profiler;
        if (profiler == null || !profiler.Measuring || _scriptOff)
            return;
        try
        {
            if (Interlocked.CompareExchange(ref _scriptThreadLogged, 1, 0) == 0)
                NameScriptThread(profiler.MainThreadId);
            _scriptStart = Stopwatch.GetTimestamp();
            _scriptOpen = true;
        }
        catch (Exception ex) { _scriptOff = true; HookFault("script", ex); }
    }

    internal static void OnScriptTickExit(ref ProbeState state)
    {
        if (!Pair(ref state, ProbeBracket.Script, HitchProbeLines.ScriptPrefixMissingLine) || !_scriptOpen)
            return;
        _scriptOpen = false;
        var profiler = MissionTickProfilerHooks.Profiler;
        if (profiler == null || !profiler.Measuring || _scriptOff)
            return;
        try { profiler.AddScriptTick(Stopwatch.GetTimestamp() - _scriptStart); }
        catch (Exception ex) { _scriptOff = true; HookFault("script", ex); }
    }

    private static void NameScriptThread(int mainThreadId)
    {
        var thread = Environment.CurrentManagedThreadId;
        var onMain = thread == mainThreadId;
        if (!onMain)
            ScriptOffMain = true;
        var line = HitchProbeLines.BuildScriptThreadLine(onMain, thread, mainThreadId);
        if (onMain)
            MissionTickProfilerHooks.Logger?.LogInfo(line);
        else
            MissionTickProfilerHooks.Logger?.LogWarning(line);
    }

    private static void ResetScriptForTests()
    {
        ScriptOffMain = false;
        _scriptOff = false;
        _scriptThreadLogged = 0;
        _scriptOpen = false;
        _scriptStart = 0;
    }
}
