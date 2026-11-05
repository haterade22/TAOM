using System;
using System.Diagnostics;

namespace TAOM.Features.MissionPerf.Hooks;

/// <summary>
/// The camera bracket: <c>MissionScreen</c>'s explicit <c>IMissionSystemHandler.UpdateCamera</c>, which runs
/// <c>CameraTick</c> and the native <c>MBWindowManager.PreDisplay()</c> inside <c>Mission.OnTick</c>; the engine
/// calls <c>PreDisplay()</c> only while the mission is Continuing and not MissionEnded, so after a battle's result
/// the bracket times <c>CameraTick</c> alone. Main thread only, same shape as the on-tick bracket; the cost is
/// one Harmony prefix and one finalizer per call, plus PatchShield's finalizer (the target is shielded from a
/// process's second game start), around two <see cref="Stopwatch.GetTimestamp"/> calls.
/// </summary>
public static partial class HitchProbeHooks
{
    private static bool _cameraOff, _cameraOpen;
    private static long _cameraStart;

    internal static void OnCameraEnter(out ProbeState state)
    {
        state = ProbeState.Entered;
        var profiler = MissionTickProfilerHooks.Profiler;
        if (profiler == null || !profiler.Measuring || _cameraOff)
            return;
        try { _cameraStart = Stopwatch.GetTimestamp(); _cameraOpen = true; }
        catch (Exception ex) { _cameraOff = true; HookFault("camera", ex); }
    }

    internal static void OnCameraExit(ref ProbeState state)
    {
        if (!Pair(ref state, ProbeBracket.Camera, HitchProbeLines.CameraPrefixMissingLine) || !_cameraOpen)
            return;
        _cameraOpen = false;
        var profiler = MissionTickProfilerHooks.Profiler;
        if (profiler == null || !profiler.Measuring || _cameraOff)
            return;
        try { profiler.AddCamera(Stopwatch.GetTimestamp() - _cameraStart); }
        catch (Exception ex) { _cameraOff = true; HookFault("camera", ex); }
    }

    private static void ResetCameraForTests()
    {
        _cameraOff = _cameraOpen = false;
        _cameraStart = 0;
    }
}
