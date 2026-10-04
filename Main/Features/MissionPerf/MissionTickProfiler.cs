using System;
using System.Collections.Generic;
using System.Threading;

namespace TAOM.Features.MissionPerf;

/// <summary>Which timed behaviour loop a call came from.</summary>
public enum TickPhase
{
    PreDisplay,
    MissionTick,
    PreTick,
}

/// <summary>
/// The frame, window and mission arithmetic of the Patch97 tick profiler. Pure: timestamps, byte
/// counts and GC counts come in as numbers, so every rule is unit-tested without the engine.
///
/// A frame is one mission tick, boundary to boundary (the <c>Mission.OnPreTick</c> prefix calls
/// <see cref="CloseFrame"/>). The first boundary of a mission only stamps the clock: whatever ran before
/// it is discarded. Behaviour calls and phase sums accumulate in the open frame and reach the window and
/// the mission totals only when that frame closes, so every total covers the same closed frames.
///
/// Threads: everything runs on the main thread except <see cref="AddAgentTick"/>, which the agent-tick
/// bracket calls from whichever thread ran <c>TickAgentsAndTeamsImp</c>, and the Patch98 any-thread adders
/// (<see cref="AddScriptTick"/>, <see cref="AddScriptParallel"/>, <see cref="AddOccasional"/>,
/// <see cref="CountOffMainSpawn"/>); their totals are <see cref="Interlocked"/>. No locks.
///
/// Modes (Patch98, plan 041): with <see cref="BehaviorTiming"/> on ("full"), the phase columns are the
/// timed behaviour sums; off ("probe"), no behaviour call is timed and <see cref="CloseFrame"/> derives them
/// from the whole-method brackets: preDisplay 0, missionTick = onTick minus the agent tick that ran on the
/// main thread, preTick = the whole OnPreTick minus the wait. Each hitch frame also carries a
/// <see cref="HitchDetailFrame"/> with the raw brackets and the spawn, script and clip-loading share.
/// </summary>
public sealed class MissionTickProfiler
{
    private readonly long _ticksPerSecond;
    private volatile bool _measuring;
    private double _hitchThresholdMs;

    private bool _haveBoundary;
    private long _lastBoundaryTicks;
    private long _lastAlloc;
    private int _lastGc0;
    private int _lastGc1;
    private int _lastGc2;

    private long _framePreDisplay;
    private long _frameMissionTick;
    private long _framePreTick;
    private long _frameWait;
    private long _agentTicks;
    private long _agentMainTicks;

    private Totals _window;
    private Totals _mission;
    private int _hitches;
    private double _worstHitchMs;
    private double _worstHitchTSeconds;

    // Patch98 open-frame brackets. Main thread: on-tick, pre-tick, spawns, the clip-loading mark.
    // Any thread (Interlocked, exchanged at CloseFrame): script tick, its calls, the two block kinds, off-main spawns.
    private long _frameOnTick;
    private long _framePreTickAll;
    private int _frameSpawns;
    private long _frameSpawnTicks;
    private bool _frameAnimLoading;
    private long _frameScriptTicks;
    private long _frameScriptCalls;
    private long _frameScriptParallel;
    private long _frameOccasional;
    private long _frameOffMainSpawns;

    private Extras _extrasWindow;
    private Extras _extrasMission;
    private bool _missionAnimSampled;

    public MissionTickProfiler(long ticksPerSecond)
    {
        _ticksPerSecond = ticksPerSecond > 0 ? ticksPerSecond : 1;
    }

    public BehaviorTickTable Behaviors { get; } = new BehaviorTickTable();

    /// <summary>Per behaviour type, the <c>OnAgentBuild</c> calls inside <c>Mission.SpawnAgent</c> (full mode).</summary>
    public BehaviorTickTable SpawnBuilds { get; } = new BehaviorTickTable();

    /// <summary>Per component type, the main-thread <c>ScriptComponentBehavior.OnTick</c> calls (full mode).</summary>
    public BehaviorTickTable ScriptComponents { get; } = new BehaviorTickTable();

    /// <summary>True when behaviour calls are timed by type ("full"); false in probe mode. Set by <see cref="BeginMission"/>.</summary>
    public bool BehaviorTiming { get; private set; }

    /// <summary>Spawn callbacks are attributed by type this mission (set at mission start; default false).</summary>
    public bool SpawnAttribution { get; set; }

    /// <summary>Main-thread script components are attributed by type this mission (set at mission start; default false).</summary>
    public bool ScriptAttribution { get; set; }

    /// <summary>The parallel and occasional script blocks are timed this mission, so their columns are not <c>na</c>.</summary>
    public bool ScriptBlockTiming { get; set; }

    /// <summary>The clip-loading sampler marks frames this mission; off, <c>animLoading</c> reads <c>na</c>.</summary>
    public bool AnimSampling { get; set; }

    public bool Measuring => _measuring;

    public int MainThreadId { get; private set; }

    public long MissionStartTicks { get; private set; }

    public int Generation { get; private set; }

    /// <summary>Hitch frames per mission that come back from <see cref="CloseFrame"/> to be written in full
    /// (D6: sample the first occurrences); every later one is still counted in the mission totals.</summary>
    public const int MaxHitchLinesPerMission = 100;

    /// <summary>True only after the <see cref="CloseFrame"/> whose hitch was the first past the cap.</summary>
    public bool HitchCapReachedThisFrame { get; private set; }

    /// <summary>Starts a mission: resets every accumulator and returns the new generation. Without
    /// <paramref name="behaviorTiming"/> (probe mode) the phase columns come from the Patch98 brackets.
    /// The attribution and sampling flags go back to false; the caller sets them after this call.</summary>
    public int BeginMission(long nowTicks, int mainThreadId, bool measuring, double hitchThresholdMs, bool behaviorTiming = true)
    {
        _measuring = false;
        BehaviorTiming = behaviorTiming;
        SpawnAttribution = false;
        ScriptAttribution = false;
        ScriptBlockTiming = false;
        AnimSampling = false;
        ZeroProbeFrame();
        _extrasWindow = default;
        _extrasMission = default;
        _missionAnimSampled = false;
        ResetTables(SpawnBuilds);
        ResetTables(ScriptComponents);
        MissionStartTicks = nowTicks;
        MainThreadId = mainThreadId;
        _hitchThresholdMs = hitchThresholdMs;
        _haveBoundary = false;
        _lastBoundaryTicks = 0;
        _lastAlloc = 0;
        _lastGc0 = _lastGc1 = _lastGc2 = 0;
        ZeroFramePhases();
        Interlocked.Exchange(ref _agentTicks, 0);
        Interlocked.Exchange(ref _agentMainTicks, 0);
        _window = default;
        _mission = default;
        _hitches = 0;
        _worstHitchMs = 0;
        _worstHitchTSeconds = 0;
        HitchCapReachedThisFrame = false;
        Behaviors.ResetFrame();
        Behaviors.ResetWindow();
        Behaviors.ResetMission();
        Generation++;
        _measuring = measuring;
        return Generation;
    }

    /// <summary>Stops measuring when <paramref name="generation"/> is still the current mission's;
    /// false (and nothing changes) for an older mission's end.</summary>
    public bool EndMission(int generation)
    {
        if (generation != Generation)
            return false;
        _measuring = false;
        return true;
    }

    public void Record(TickPhase phase, int slot, long elapsedTicks, long allocBytes)
    {
        switch (phase)
        {
            case TickPhase.PreDisplay: _framePreDisplay += elapsedTicks; break;
            case TickPhase.MissionTick: _frameMissionTick += elapsedTicks; break;
            default: _framePreTick += elapsedTicks; break;
        }
        Behaviors.Record(slot, elapsedTicks, allocBytes);
    }

    public void AddWait(long elapsedTicks) => _frameWait += elapsedTicks;

    /// <summary>The one member callable off the main thread.</summary>
    public void AddAgentTick(long elapsedTicks, bool onMainThread)
    {
        Interlocked.Add(ref _agentTicks, elapsedTicks);
        if (onMainThread)
            Interlocked.Add(ref _agentMainTicks, elapsedTicks);
    }

    /// <summary>The whole <c>Mission.OnTick</c> bracket. Main thread.</summary>
    public void AddOnTick(long elapsedTicks) => _frameOnTick += elapsedTicks;

    /// <summary>The whole <c>Mission.OnPreTick</c> bracket (the wait included). Main thread.</summary>
    public void AddPreTickAll(long elapsedTicks) => _framePreTickAll += elapsedTicks;

    /// <summary>One <c>Mission.SpawnAgent</c> call on the main thread, nested ones included. Main thread.</summary>
    public void CountSpawn() => _frameSpawns++;

    /// <summary>The time of an outermost <c>Mission.SpawnAgent</c> call. Main thread.</summary>
    public void AddSpawnTime(long elapsedTicks) => _frameSpawnTicks += elapsedTicks;

    /// <summary>Whether a clip was loading from disk when the open frame began. Main thread.</summary>
    public void MarkAnimLoading(bool loading) => _frameAnimLoading = loading;

    /// <summary>One <c>ManagedScriptHolder.TickComponents</c> call and its time. Any thread.</summary>
    public void AddScriptTick(long elapsedTicks)
    {
        Interlocked.Add(ref _frameScriptTicks, elapsedTicks);
        Interlocked.Increment(ref _frameScriptCalls);
    }

    /// <summary>One of the three parallel script blocks inside <c>TickComponents</c>. Any thread.</summary>
    public void AddScriptParallel(long elapsedTicks) => Interlocked.Add(ref _frameScriptParallel, elapsedTicks);

    /// <summary>The occasional script block inside <c>TickComponents</c>. Any thread.</summary>
    public void AddOccasional(long elapsedTicks) => Interlocked.Add(ref _frameOccasional, elapsedTicks);

    /// <summary>A <c>Mission.SpawnAgent</c> call off the main thread: counted, not timed. Any thread.</summary>
    public void CountOffMainSpawn() => Interlocked.Increment(ref _frameOffMainSpawns);

    /// <summary>Closes the open frame at a boundary. Returns the frame when it is one of the mission's first
    /// <see cref="MaxHitchLinesPerMission"/> hitches, else null (allocating nothing); every hitch is counted.</summary>
    public HitchFrame? CloseFrame(long nowTicks, long allocBytesNow, int gc0, int gc1, int gc2)
    {
        HitchCapReachedThisFrame = false;
        var agent = Interlocked.Exchange(ref _agentTicks, 0);
        var agentMain = Interlocked.Exchange(ref _agentMainTicks, 0);
        var frame = TakeProbeFrame();

        if (!_haveBoundary)
        {
            // Spawns before the mission's first boundary (AfterStart spawns) are kept as pre-frame totals.
            _haveBoundary = true;
            _extrasMission.PreFrameSpawns += frame.Spawns;
            _extrasMission.PreFrameSpawnTicks += frame.SpawnTicks;
            _extrasMission.OffMainSpawns += frame.OffMainSpawns;
            Stamp(nowTicks, allocBytesNow, gc0, gc1, gc2);
            ZeroFramePhases();
            Behaviors.ResetFrame();
            SpawnBuilds.ResetFrame();
            ScriptComponents.ResetFrame();
            return null;
        }

        var frameMs = ToMs(nowTicks - _lastBoundaryTicks);
        var preDisplayMs = ToMs(_framePreDisplay);
        var missionTickMs = ToMs(_frameMissionTick);
        var preTickMs = ToMs(_framePreTick);
        var waitMs = ToMs(_frameWait);
        var agentMs = ToMs(agent);
        if (!BehaviorTiming)
        {
            preDisplayMs = 0d;
            missionTickMs = Math.Max(0d, ToMs(frame.OnTick - agentMain));
            preTickMs = Math.Max(0d, ToMs(frame.PreTickAll - _frameWait));
        }
        var otherMs = Math.Max(0d, frameMs - preDisplayMs - missionTickMs - preTickMs - waitMs - ToMs(agentMain));
        var allocBytes = allocBytesNow - _lastAlloc;
        var loading = AnimSampling && frame.AnimLoading;

        HitchFrame? hitch = null;
        if (frameMs >= _hitchThresholdMs)
        {
            _hitches++;
            if (loading)
                _extrasMission.HitchesWithAnimLoading++;
            if (_hitches <= MaxHitchLinesPerMission)
                hitch = new HitchFrame(frameMs, preDisplayMs, missionTickMs, preTickMs, waitMs, agentMs, otherMs,
                    gc0 - _lastGc0, gc1 - _lastGc1, gc2 - _lastGc2, allocBytes, Behaviors.FrameTop(3, _ticksPerSecond))
                {
                    Detail = BuildDetail(in frame),
                };
            else
                HitchCapReachedThisFrame = _hitches == MaxHitchLinesPerMission + 1;
            if (frameMs > _worstHitchMs)
            {
                _worstHitchMs = frameMs;
                _worstHitchTSeconds = (nowTicks - MissionStartTicks) / (double)_ticksPerSecond;
            }
        }

        Behaviors.FoldFrame();
        SpawnBuilds.FoldFrame();
        ScriptComponents.FoldFrame();
        _window.Add(frameMs, preDisplayMs, missionTickMs, preTickMs, waitMs, agentMs, otherMs, allocBytes);
        _mission.Add(frameMs, preDisplayMs, missionTickMs, preTickMs, waitMs, agentMs, otherMs, allocBytes);
        _extrasWindow.Add(in frame, loading);
        _extrasMission.Add(in frame, loading);
        if (AnimSampling)
            _missionAnimSampled = true;
        Stamp(nowTicks, allocBytesNow, gc0, gc1, gc2);
        ZeroFramePhases();
        return hitch;
    }

    /// <summary>The closed frames since the last call, for the Patch98 window lines, then resets that window
    /// and both attribution tables' windows. Call right after <see cref="TakeWindow"/> so both cover the same frames.</summary>
    public ExtrasWindow TakeExtrasWindow(int topN)
    {
        var e = _extrasWindow;
        var window = new ExtrasWindow(e.Spawns, ToMs(e.SpawnTicks),
            SpawnAttribution ? SpawnBuilds.WindowTop(topN, _ticksPerSecond) : Array.Empty<BehaviorTotal>(),
            e.ScriptCalls, ToMs(e.ScriptTicks), BlockMs(e.ParallelTicks), BlockMs(e.OccasionalTicks),
            ScriptAttribution ? ScriptComponents.WindowTop(topN, _ticksPerSecond) : Array.Empty<BehaviorTotal>(),
            e.Frames, e.LoadingFrames, AnimSampling);
        _extrasWindow = default;
        SpawnBuilds.ResetWindow();
        ScriptComponents.ResetWindow();
        return window;
    }

    /// <summary>Every closed frame of the mission so far, for <c>[TickSummaryExtra]</c>; resets nothing.</summary>
    public MissionExtras SummarizeExtras(int topN)
    {
        var m = _extrasMission;
        return new MissionExtras(ToMs(m.SpawnTicks), ToMs(m.ScriptTicks),
            _missionAnimSampled ? m.LoadingFrames : -1, _missionAnimSampled ? m.HitchesWithAnimLoading : -1,
            ModeName, m.Frames, m.Spawns, m.PreFrameSpawns, ToMs(m.PreFrameSpawnTicks), m.OffMainSpawns,
            BlockMs(m.ParallelTicks), BlockMs(m.OccasionalTicks), ToMs(m.OnTickTicks), ToMs(m.PreTickAllTicks),
            SpawnAttribution ? SpawnBuilds.MissionTop(topN, _ticksPerSecond) : Array.Empty<BehaviorTotal>(),
            ScriptAttribution ? ScriptComponents.MissionTop(topN, _ticksPerSecond) : Array.Empty<BehaviorTotal>());
    }

    private string ModeName => BehaviorTiming ? "full" : "probe";

    private double BlockMs(long ticks) => ScriptBlockTiming ? ToMs(ticks) : double.NaN;

    private HitchDetailFrame BuildDetail(in ProbeFrame f) => new HitchDetailFrame(ToMs(f.SpawnTicks), ToMs(f.ScriptTicks),
        BlockMs(f.ParallelTicks), AnimSampling ? (f.AnimLoading ? 1 : 0) : -1, ModeName, f.Spawns, BlockMs(f.OccasionalTicks),
        ToMs(f.OnTick), ToMs(f.PreTickAll));

    private ProbeFrame TakeProbeFrame()
    {
        var f = new ProbeFrame
        {
            OnTick = _frameOnTick,
            PreTickAll = _framePreTickAll,
            Spawns = _frameSpawns,
            SpawnTicks = _frameSpawnTicks,
            AnimLoading = _frameAnimLoading,
            ScriptTicks = Interlocked.Exchange(ref _frameScriptTicks, 0),
            ScriptCalls = (int)Interlocked.Exchange(ref _frameScriptCalls, 0),
            ParallelTicks = Interlocked.Exchange(ref _frameScriptParallel, 0),
            OccasionalTicks = Interlocked.Exchange(ref _frameOccasional, 0),
            OffMainSpawns = (int)Interlocked.Exchange(ref _frameOffMainSpawns, 0),
        };
        _frameOnTick = 0;
        _framePreTickAll = 0;
        _frameSpawns = 0;
        _frameSpawnTicks = 0;
        _frameAnimLoading = false;
        return f;
    }

    private void ZeroProbeFrame() => TakeProbeFrame();

    private static void ResetTables(BehaviorTickTable table)
    {
        table.ResetFrame();
        table.ResetWindow();
        table.ResetMission();
    }

    /// <summary>The closed frames since the last call, then resets the window. Never touches the open frame.</summary>
    public TickWindow TakeWindow(int topN)
    {
        var w = _window;
        var window = new TickWindow(w.Frames, w.WallMs, w.PreDisplayMs, w.MissionTickMs, w.PreTickMs, w.WaitMs,
            w.AgentMs, w.OtherMs, w.AllocBytes, Behaviors.WindowTop(topN, _ticksPerSecond));
        _window = default;
        Behaviors.ResetWindow();
        return window;
    }

    /// <summary>Every closed frame of the mission so far, for <c>[TickSummary]</c>; resets nothing.</summary>
    public TickSummary Summarize(int topN)
    {
        var m = _mission;
        return new TickSummary(m.Frames, m.WallMs, m.PreDisplayMs, m.MissionTickMs, m.PreTickMs, m.WaitMs,
            m.AgentMs, m.OtherMs, m.AllocBytes, _hitches, _worstHitchMs, _worstHitchTSeconds,
            Behaviors.MissionTop(topN, _ticksPerSecond));
    }

    private double ToMs(long ticks) => ticks * 1000d / _ticksPerSecond;

    private void Stamp(long nowTicks, long alloc, int gc0, int gc1, int gc2)
    {
        _lastBoundaryTicks = nowTicks;
        _lastAlloc = alloc;
        _lastGc0 = gc0;
        _lastGc1 = gc1;
        _lastGc2 = gc2;
    }

    private void ZeroFramePhases()
    {
        _framePreDisplay = 0;
        _frameMissionTick = 0;
        _framePreTick = 0;
        _frameWait = 0;
    }

    private struct Totals
    {
        public int Frames;
        public double WallMs;
        public double PreDisplayMs;
        public double MissionTickMs;
        public double PreTickMs;
        public double WaitMs;
        public double AgentMs;
        public double OtherMs;
        public long AllocBytes;

        public void Add(double frameMs, double preDisplayMs, double missionTickMs, double preTickMs, double waitMs,
            double agentMs, double otherMs, long allocBytes)
        {
            Frames++;
            WallMs += frameMs;
            PreDisplayMs += preDisplayMs;
            MissionTickMs += missionTickMs;
            PreTickMs += preTickMs;
            WaitMs += waitMs;
            AgentMs += agentMs;
            OtherMs += otherMs;
            AllocBytes += allocBytes;
        }
    }

    /// <summary>One closed frame's Patch98 brackets, taken from the open-frame fields.</summary>
    private struct ProbeFrame
    {
        public long OnTick;
        public long PreTickAll;
        public int Spawns;
        public long SpawnTicks;
        public bool AnimLoading;
        public long ScriptTicks;
        public int ScriptCalls;
        public long ParallelTicks;
        public long OccasionalTicks;
        public int OffMainSpawns;
    }

    /// <summary>The Patch98 totals over a window or a mission, in ticks and counts.</summary>
    private struct Extras
    {
        public int Frames;
        public int Spawns;
        public long SpawnTicks;
        public int ScriptCalls;
        public long ScriptTicks;
        public long ParallelTicks;
        public long OccasionalTicks;
        public long OnTickTicks;
        public long PreTickAllTicks;
        public int LoadingFrames;
        public int OffMainSpawns;
        public int HitchesWithAnimLoading;
        public int PreFrameSpawns;
        public long PreFrameSpawnTicks;

        public void Add(in ProbeFrame f, bool loading)
        {
            Frames++;
            Spawns += f.Spawns;
            SpawnTicks += f.SpawnTicks;
            ScriptCalls += f.ScriptCalls;
            ScriptTicks += f.ScriptTicks;
            ParallelTicks += f.ParallelTicks;
            OccasionalTicks += f.OccasionalTicks;
            OnTickTicks += f.OnTick;
            PreTickAllTicks += f.PreTickAll;
            OffMainSpawns += f.OffMainSpawns;
            if (loading)
                LoadingFrames++;
        }
    }
}

/// <summary>The closed frames of one <c>[TickProfile]</c> window, fields in line order.</summary>
public sealed class TickWindow
{
    public TickWindow(int frames, double wallMs, double preDisplayMs, double missionTickMs, double preTickMs,
        double waitTickMs, double agentTickMs, double otherMs, long allocBytes, IReadOnlyList<BehaviorTotal> top)
    {
        Frames = frames;
        WallMs = wallMs;
        PreDisplayMs = preDisplayMs;
        MissionTickMs = missionTickMs;
        PreTickMs = preTickMs;
        WaitTickMs = waitTickMs;
        AgentTickMs = agentTickMs;
        OtherMs = otherMs;
        AllocBytes = allocBytes;
        Top = top;
    }

    public int Frames { get; }
    public double WallMs { get; }
    public double PreDisplayMs { get; }
    public double MissionTickMs { get; }
    public double PreTickMs { get; }
    public double WaitTickMs { get; }
    public double AgentTickMs { get; }
    public double OtherMs { get; }
    public long AllocBytes { get; }
    public IReadOnlyList<BehaviorTotal> Top { get; }
}

/// <summary>One frame at or above the hitch threshold, fields in <c>[Hitch]</c> line order.</summary>
public sealed class HitchFrame
{
    public HitchFrame(double frameMs, double preDisplayMs, double missionTickMs, double preTickMs, double waitTickMs,
        double agentTickMs, double otherMs, int gc0, int gc1, int gc2, long allocBytes, IReadOnlyList<BehaviorTotal> top)
    {
        FrameMs = frameMs;
        PreDisplayMs = preDisplayMs;
        MissionTickMs = missionTickMs;
        PreTickMs = preTickMs;
        WaitTickMs = waitTickMs;
        AgentTickMs = agentTickMs;
        OtherMs = otherMs;
        Gc0 = gc0;
        Gc1 = gc1;
        Gc2 = gc2;
        AllocBytes = allocBytes;
        Top = top;
    }

    public double FrameMs { get; }
    public double PreDisplayMs { get; }
    public double MissionTickMs { get; }
    public double PreTickMs { get; }
    public double WaitTickMs { get; }
    public double AgentTickMs { get; }
    public double OtherMs { get; }
    public int Gc0 { get; }
    public int Gc1 { get; }
    public int Gc2 { get; }
    public long AllocBytes { get; }
    public IReadOnlyList<BehaviorTotal> Top { get; }

    /// <summary>The frame's Patch98 brackets for its <c>[HitchDetail]</c> line; set by <see cref="MissionTickProfiler.CloseFrame"/>.</summary>
    public HitchDetailFrame? Detail { get; internal set; }
}

/// <summary>Every closed frame of a mission, fields in <c>[TickSummary]</c> line order.</summary>
public sealed class TickSummary
{
    public TickSummary(int frames, double wallMs, double preDisplayMs, double missionTickMs, double preTickMs,
        double waitTickMs, double agentTickMs, double otherMs, long allocBytes, int hitches, double worstHitchMs,
        double worstHitchTSeconds, IReadOnlyList<BehaviorTotal> top)
    {
        Frames = frames;
        WallMs = wallMs;
        PreDisplayMs = preDisplayMs;
        MissionTickMs = missionTickMs;
        PreTickMs = preTickMs;
        WaitTickMs = waitTickMs;
        AgentTickMs = agentTickMs;
        OtherMs = otherMs;
        AllocBytes = allocBytes;
        Hitches = hitches;
        WorstHitchMs = worstHitchMs;
        WorstHitchTSeconds = worstHitchTSeconds;
        Top = top;
    }

    public int Frames { get; }
    public double WallMs { get; }
    public double PreDisplayMs { get; }
    public double MissionTickMs { get; }
    public double PreTickMs { get; }
    public double WaitTickMs { get; }
    public double AgentTickMs { get; }
    public double OtherMs { get; }
    public long AllocBytes { get; }
    public int Hitches { get; }
    public double WorstHitchMs { get; }
    public double WorstHitchTSeconds { get; }
    public IReadOnlyList<BehaviorTotal> Top { get; }
}
