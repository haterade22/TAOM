using System;
using System.Diagnostics;
using System.Threading;

namespace TAOM.Features.MissionPerf.Hooks;

/// <summary>
/// The <c>Mission.SpawnAgent</c> bracket. Spawns nest (an <c>OnAgentBuild</c> handler may spawn), so a
/// main-thread depth counter times only the outermost call into <c>spawnMs</c>, and every call counts in
/// <c>spawns</c>; the finalizer closes the bracket when the spawn throws. A call off the main thread is counted
/// in <c>offMainSpawns</c>, not timed, with one WARNING line per process. A finalizer whose own call had no
/// prefix (see <see cref="HitchProbeHooks"/>) records nothing, so a nested call that lost its prefix cannot close
/// the outer call that kept its own; it is counted in the missing-prefix total, and the first one per process
/// writes the missing-prefix WARNING line.
/// </summary>
public static partial class HitchProbeHooks
{
    private static bool _spawnOff;
    private static int _spawnDepth;
    private static long _spawnStart;
    private static int _spawnOffMainLogged;

    internal static void OnSpawnEnter(out ProbeState state)
    {
        state = ProbeState.Entered;
        var profiler = MissionTickProfilerHooks.Profiler;
        if (profiler == null || !profiler.Measuring || _spawnOff)
            return;
        try
        {
            var thread = Environment.CurrentManagedThreadId;
            if (thread != profiler.MainThreadId)
            {
                profiler.CountOffMainSpawn();
                if (Interlocked.CompareExchange(ref _spawnOffMainLogged, 1, 0) == 0)
                    MissionTickProfilerHooks.Logger?.LogWarning(HitchProbeLines.BuildSpawnOffMainLine(thread));
                return;
            }
            profiler.CountSpawn();
            if (_spawnDepth++ == 0)
                _spawnStart = Stopwatch.GetTimestamp();
        }
        catch (Exception ex) { _spawnOff = true; HookFault("spawn", ex); }
    }

    internal static void OnSpawnExit(ref ProbeState state)
    {
        if (!Pair(ref state, ProbeBracket.Spawn, HitchProbeLines.SpawnPrefixMissingLine))
            return;
        var profiler = MissionTickProfilerHooks.Profiler;
        if (_spawnDepth == 0 || profiler == null || Environment.CurrentManagedThreadId != profiler.MainThreadId)
            return;
        try
        {
            if (--_spawnDepth == 0 && profiler.Measuring && !_spawnOff)
                profiler.AddSpawnTime(Stopwatch.GetTimestamp() - _spawnStart);
        }
        catch (Exception ex) { _spawnOff = true; HookFault("spawn", ex); }
    }

    private static void ResetSpawnForTests()
    {
        _spawnOff = false;
        _spawnDepth = 0;
        _spawnStart = 0;
        _spawnOffMainLogged = 0;
    }
}
