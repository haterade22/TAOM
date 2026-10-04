using System;
using System.Diagnostics;

namespace TAOM.Features.MissionPerf;

/// <summary>
/// Measures, at game start, what the Patch98 hooks' profiler bookkeeping costs per frame: on a scratch
/// <see cref="MissionTickProfiler"/> in probe mode (never the live one), it replays the calls the hooks make in
/// one frame (the frame boundary with its allocation and GC reads, the clip-loading mark, the wait, pre-tick,
/// on-tick, agent-tick and script-tick brackets, two spawns) with each bracket's two timestamps, and returns
/// the average microseconds per frame. Harmony's own call overhead is not in it; the overhead benchmark
/// (<c>HitchProbeOverheadBenchmarkTests</c>) measures that. Pure: no engine type.
/// </summary>
public static class ProbeCostMeter
{
    private const int WarmUpFrames = 200;

    public static double MeasureBookkeepingMicroseconds(int frames)
    {
        if (frames <= 0)
            return 0d;
        var profiler = new MissionTickProfiler(Stopwatch.Frequency);
        profiler.BeginMission(Stopwatch.GetTimestamp(), Environment.CurrentManagedThreadId, measuring: true,
            hitchThresholdMs: double.MaxValue, behaviorTiming: false);
        for (var i = 0; i < WarmUpFrames; i++)
            Frame(profiler);
        var start = Stopwatch.GetTimestamp();
        for (var i = 0; i < frames; i++)
            Frame(profiler);
        var elapsed = Stopwatch.GetTimestamp() - start;
        return elapsed * 1_000_000d / Stopwatch.Frequency / frames;
    }

    private static void Frame(MissionTickProfiler p)
    {
        p.CloseFrame(Stopwatch.GetTimestamp(), AllocationCounter.ReadOrZero(),
            GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2));
        p.MarkAnimLoading(false);
        p.AddWait(Bracket());
        p.AddPreTickAll(Bracket());
        p.AddOnTick(Bracket());
        p.AddAgentTick(Bracket(), false);
        p.AddScriptTick(Bracket());
        p.CountSpawn();
        p.CountSpawn();
        p.AddSpawnTime(Bracket());
    }

    private static long Bracket()
    {
        var t0 = Stopwatch.GetTimestamp();
        return Stopwatch.GetTimestamp() - t0;
    }
}
