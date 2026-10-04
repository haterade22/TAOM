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
/// bracket calls from whichever thread ran <c>TickAgentsAndTeamsImp</c>; its two totals are
/// <see cref="Interlocked"/>. No locks.
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

    public MissionTickProfiler(long ticksPerSecond)
    {
        _ticksPerSecond = ticksPerSecond > 0 ? ticksPerSecond : 1;
    }

    public BehaviorTickTable Behaviors { get; } = new BehaviorTickTable();

    public bool Measuring => _measuring;

    public int MainThreadId { get; private set; }

    public long MissionStartTicks { get; private set; }

    public int Generation { get; private set; }

    /// <summary>Hitch frames per mission that come back from <see cref="CloseFrame"/> to be written in full
    /// (D6: sample the first occurrences); every later one is still counted in the mission totals.</summary>
    public const int MaxHitchLinesPerMission = 100;

    /// <summary>True only after the <see cref="CloseFrame"/> whose hitch was the first past the cap.</summary>
    public bool HitchCapReachedThisFrame { get; private set; }

    /// <summary>Starts a mission: resets every accumulator and returns the new generation.</summary>
    public int BeginMission(long nowTicks, int mainThreadId, bool measuring, double hitchThresholdMs)
    {
        _measuring = false;
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

    /// <summary>Closes the open frame at a boundary. Returns the frame when it is one of the mission's first
    /// <see cref="MaxHitchLinesPerMission"/> hitches, else null (allocating nothing); every hitch is counted.</summary>
    public HitchFrame? CloseFrame(long nowTicks, long allocBytesNow, int gc0, int gc1, int gc2)
    {
        HitchCapReachedThisFrame = false;
        var agent = Interlocked.Exchange(ref _agentTicks, 0);
        var agentMain = Interlocked.Exchange(ref _agentMainTicks, 0);

        if (!_haveBoundary)
        {
            _haveBoundary = true;
            Stamp(nowTicks, allocBytesNow, gc0, gc1, gc2);
            ZeroFramePhases();
            Behaviors.ResetFrame();
            return null;
        }

        var frameMs = ToMs(nowTicks - _lastBoundaryTicks);
        var preDisplayMs = ToMs(_framePreDisplay);
        var missionTickMs = ToMs(_frameMissionTick);
        var preTickMs = ToMs(_framePreTick);
        var waitMs = ToMs(_frameWait);
        var agentMs = ToMs(agent);
        var otherMs = Math.Max(0d, frameMs - preDisplayMs - missionTickMs - preTickMs - waitMs - ToMs(agentMain));
        var allocBytes = allocBytesNow - _lastAlloc;

        HitchFrame? hitch = null;
        if (frameMs >= _hitchThresholdMs)
        {
            _hitches++;
            if (_hitches <= MaxHitchLinesPerMission)
                hitch = new HitchFrame(frameMs, preDisplayMs, missionTickMs, preTickMs, waitMs, agentMs, otherMs,
                    gc0 - _lastGc0, gc1 - _lastGc1, gc2 - _lastGc2, allocBytes, Behaviors.FrameTop(3, _ticksPerSecond));
            else
                HitchCapReachedThisFrame = _hitches == MaxHitchLinesPerMission + 1;
            if (frameMs > _worstHitchMs)
            {
                _worstHitchMs = frameMs;
                _worstHitchTSeconds = (nowTicks - MissionStartTicks) / (double)_ticksPerSecond;
            }
        }

        Behaviors.FoldFrame();
        _window.Add(frameMs, preDisplayMs, missionTickMs, preTickMs, waitMs, agentMs, otherMs, allocBytes);
        _mission.Add(frameMs, preDisplayMs, missionTickMs, preTickMs, waitMs, agentMs, otherMs, allocBytes);
        Stamp(nowTicks, allocBytesNow, gc0, gc1, gc2);
        ZeroFramePhases();
        return hitch;
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
