using System;
using System.Collections.Generic;

namespace TAOM.Features.MissionPerf;

/// <summary>
/// One slow frame's Patch98 brackets, fields in <c>[HitchDetail]</c> line order. <see cref="ScriptParallelMs"/>
/// and <see cref="OccasionalMs"/> are <see cref="double.NaN"/> when not measured (written <c>na</c>);
/// <see cref="AnimLoading"/> is 1 or 0 while the clip-loading sampler is on, and -1 when it is off.
/// </summary>
public sealed class HitchDetailFrame
{
    public HitchDetailFrame(double spawnMs, double scriptTickMs, double scriptParallelMs, int animLoading, string mode,
        int spawns, double occasionalMs, double onTickMs, double preTickAllMs)
    {
        SpawnMs = spawnMs;
        ScriptTickMs = scriptTickMs;
        ScriptParallelMs = scriptParallelMs;
        AnimLoading = animLoading;
        Mode = mode;
        Spawns = spawns;
        OccasionalMs = occasionalMs;
        OnTickMs = onTickMs;
        PreTickAllMs = preTickAllMs;
    }

    public double SpawnMs { get; }
    public double ScriptTickMs { get; }
    public double ScriptParallelMs { get; }
    public int AnimLoading { get; }
    public string Mode { get; }
    public int Spawns { get; }
    public double OccasionalMs { get; }
    public double OnTickMs { get; }
    public double PreTickAllMs { get; }
}

/// <summary>
/// The closed frames of one 5 s window, for <c>[SpawnProfile]</c>, <c>[ScriptProfile]</c> and <c>[AnimLoad]</c>.
/// NaN means not measured; an empty top list means no attribution.
/// </summary>
public sealed class ExtrasWindow
{
    public ExtrasWindow(int spawns, double spawnMs, IReadOnlyList<BehaviorTotal> spawnTop, int scriptCalls,
        double scriptTickMs, double scriptParallelMs, double occasionalMs, IReadOnlyList<BehaviorTotal> scriptTop,
        int frames, int loadingFrames, bool animSampling)
    {
        Spawns = spawns;
        SpawnMs = spawnMs;
        SpawnTop = spawnTop;
        ScriptCalls = scriptCalls;
        ScriptTickMs = scriptTickMs;
        ScriptParallelMs = scriptParallelMs;
        OccasionalMs = occasionalMs;
        ScriptTop = scriptTop;
        Frames = frames;
        LoadingFrames = loadingFrames;
        AnimSampling = animSampling;
    }

    public int Spawns { get; }
    public double SpawnMs { get; }
    public IReadOnlyList<BehaviorTotal> SpawnTop { get; }
    public int ScriptCalls { get; }
    public double ScriptTickMs { get; }
    public double ScriptParallelMs { get; }
    public double OccasionalMs { get; }
    public IReadOnlyList<BehaviorTotal> ScriptTop { get; }
    public int Frames { get; }
    public int LoadingFrames { get; }
    public bool AnimSampling { get; }
}

/// <summary>
/// Every closed frame of a mission, fields in <c>[TickSummaryExtra]</c> line order. NaN (a double) or -1
/// (a count) means <c>na</c>; an empty top list is <c>none</c>.
/// </summary>
public sealed class MissionExtras
{
    public MissionExtras(double spawnMs, double scriptTickMs, int animLoadingFrames, int hitchesWithAnimLoading,
        string mode, int frames, int spawns, int preFrameSpawns, double preFrameSpawnMs, int offMainSpawns,
        double scriptParallelMs, double occasionalMs, double onTickMs, double preTickAllMs,
        IReadOnlyList<BehaviorTotal> spawnTop, IReadOnlyList<BehaviorTotal> scriptTop)
    {
        SpawnMs = spawnMs;
        ScriptTickMs = scriptTickMs;
        AnimLoadingFrames = animLoadingFrames;
        HitchesWithAnimLoading = hitchesWithAnimLoading;
        Mode = mode;
        Frames = frames;
        Spawns = spawns;
        PreFrameSpawns = preFrameSpawns;
        PreFrameSpawnMs = preFrameSpawnMs;
        OffMainSpawns = offMainSpawns;
        ScriptParallelMs = scriptParallelMs;
        OccasionalMs = occasionalMs;
        OnTickMs = onTickMs;
        PreTickAllMs = preTickAllMs;
        SpawnTop = spawnTop;
        ScriptTop = scriptTop;
    }

    public double SpawnMs { get; }
    public double ScriptTickMs { get; }
    public int AnimLoadingFrames { get; }
    public int HitchesWithAnimLoading { get; }
    public string Mode { get; }
    public int Frames { get; }
    public int Spawns { get; }
    public int PreFrameSpawns { get; }
    public double PreFrameSpawnMs { get; }
    public int OffMainSpawns { get; }
    public double ScriptParallelMs { get; }
    public double OccasionalMs { get; }
    public double OnTickMs { get; }
    public double PreTickAllMs { get; }
    public IReadOnlyList<BehaviorTotal> SpawnTop { get; }
    public IReadOnlyList<BehaviorTotal> ScriptTop { get; }
}
