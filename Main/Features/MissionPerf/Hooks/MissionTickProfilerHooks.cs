using System;
using System.Diagnostics;
using System.Threading;
using TAOM.Core.Logging;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.MissionPerf.Hooks;

/// <summary>
/// Patch97's helpers (called in place of the behaviour virtuals in <c>Mission.OnTick</c> and
/// <c>Mission.OnPreTick</c>), the frame boundary, Patch91's agent-tick bracket and the mission start and
/// end. Thin (ADR-002): the arithmetic is <see cref="MissionTickProfiler"/>, every line <see cref="TickProfileLines"/>.
/// Not measuring, a helper calls the virtual and returns; measuring, it times the call in try/finally, so a
/// throwing behaviour's exception propagates unchanged and is still recorded. Main thread except the
/// agent-tick pair. A fault ends measuring for the mission, so its reason is logged once, never per frame.
/// </summary>
public static class MissionTickProfilerHooks
{
    internal static MissionTickProfiler? Profiler;
    internal static IModLogger? Logger;
    internal static Action<Mission>? WaitTickCompletionCall;
    internal static bool Installed; // the startup fact only; whether the hooks are still there is MissionTickProfilerHealth
    internal static int OnTickSites;
    internal static int OnPreTickSites;

    // The open agent tick's start stamp; 0 when none is open (a QueryPerformanceCounter value is never 0). One slot: one open tick at a time (doc: Known limits).
    private static long _agentTickStart;

    public static void TimedPreDisplay(MissionBehavior behavior, float dt) => Timed(TickPhase.PreDisplay, behavior, dt);
    public static void TimedMissionTick(MissionBehavior behavior, float dt) => Timed(TickPhase.MissionTick, behavior, dt);
    public static void TimedPreMissionTick(MissionBehavior behavior, float dt) => Timed(TickPhase.PreTick, behavior, dt);

    /// <summary>Replaces <c>WaitTickCompletion()</c>; swapped in only when the delegate bound, so never null here.</summary>
    public static void TimedWaitTickCompletion(Mission mission)
    {
        var profiler = Profiler;
        if (profiler == null || !profiler.Measuring)
        {
            WaitTickCompletionCall!(mission);
            return;
        }
        var t0 = Stopwatch.GetTimestamp();
        try { WaitTickCompletionCall!(mission); }
        finally { profiler.AddWait(Stopwatch.GetTimestamp() - t0); }
    }

    internal static void OnFrameBoundary()
    {
        var profiler = Profiler;
        if (profiler == null || !profiler.Measuring)
            return;
        try
        {
            var now = Stopwatch.GetTimestamp();
            var hitch = profiler.CloseFrame(now, AllocationCounter.ReadOrZero(),
                GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2));
            if (hitch == null && !profiler.HitchCapReachedThisFrame)
                return;
            var t = (now - profiler.MissionStartTicks) / (double)Stopwatch.Frequency;
            Logger?.LogInfo(hitch != null ? TickProfileLines.BuildHitch(t, hitch, AllocationCounter.Available)
                : TickProfileLines.BuildHitchCapLine(MissionTickProfiler.MaxHitchLinesPerMission, t));
        }
        catch (Exception ex) { Fault(profiler, "frame boundary", ex); }
    }

    internal static void OnAgentTickEnter()
    {
        var profiler = Profiler;
        if (profiler == null || !profiler.Measuring)
            return;
        try { Volatile.Write(ref _agentTickStart, Stopwatch.GetTimestamp()); }
        catch (Exception ex) { Fault(profiler, "agent tick", ex); }
    }

    internal static void OnAgentTickExit()
    {
        var profiler = Profiler;
        if (profiler == null || !profiler.Measuring)
            return;
        try
        {
            var start = Interlocked.Exchange(ref _agentTickStart, 0);
            if (start != 0)
                profiler.AddAgentTick(Stopwatch.GetTimestamp() - start, Environment.CurrentManagedThreadId == profiler.MainThreadId);
        }
        catch (Exception ex) { Fault(profiler, "agent tick", ex); }
    }

    /// <summary>Starts a mission on the calling (main) thread; returns its generation, 0 when not installed.</summary>
    internal static int BeginMission(long missionStartTicks, bool measuring, double hitchThresholdMs)
    {
        var profiler = Profiler;
        if (profiler == null)
            return 0;
        Interlocked.Exchange(ref _agentTickStart, 0);
        return profiler.BeginMission(missionStartTicks, Environment.CurrentManagedThreadId, measuring, hitchThresholdMs);
    }

    /// <summary>Stops measuring for <paramref name="generation"/>; an older mission's end logs why it changed nothing.</summary>
    internal static void EndMission(int generation)
    {
        var profiler = Profiler;
        if (profiler != null && !profiler.EndMission(generation))
            Logger?.LogInfo(TickProfileLines.BuildStaleEndLine(generation, profiler.Generation));
    }

    /// <summary>The <c>[TickSummary]</c> line for the current mission, or why it has none (no frame closed).</summary>
    internal static void WriteSummary(int generation, int topN)
    {
        var profiler = Profiler;
        if (profiler == null || generation != profiler.Generation)
            return;
        try
        {
            var summary = profiler.Summarize(topN);
            Logger?.LogInfo(summary.Frames > 0 ? TickProfileLines.BuildTickSummary(summary, AllocationCounter.Available)
                : TickProfileLines.BuildNoFramesLine(generation));
        }
        catch (Exception ex) { Fault(profiler, "mission summary", ex); }
    }

    private static void Timed(TickPhase phase, MissionBehavior behavior, float dt)
    {
        var profiler = Profiler;
        if (profiler == null || !profiler.Measuring)
        {
            Call(phase, behavior, dt);
            return;
        }
        var slot = profiler.Behaviors.SlotFor(behavior.GetType());
        var a0 = AllocationCounter.ReadOrZero();
        var t0 = Stopwatch.GetTimestamp();
        try { Call(phase, behavior, dt); }
        finally { profiler.Record(phase, slot, Stopwatch.GetTimestamp() - t0, AllocationCounter.ReadOrZero() - a0); }
    }

    private static void Call(TickPhase phase, MissionBehavior behavior, float dt)
    {
        if (phase == TickPhase.MissionTick) behavior.OnMissionTick(dt);
        else if (phase == TickPhase.PreDisplay) behavior.OnPreDisplayMissionTick(dt);
        else behavior.OnPreMissionTick(dt);
    }

    private static void Fault(MissionTickProfiler profiler, string where, Exception ex)
    {
        try { profiler.EndMission(profiler.Generation); Logger?.LogError(TickProfileLines.BuildFault(where, ex)); }
        catch { /* diagnostic only: never throw into a mission tick */ }
    }
}
