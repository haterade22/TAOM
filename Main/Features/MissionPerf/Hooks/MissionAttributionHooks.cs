using System;
using System.Diagnostics;
using System.Threading;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.MissionPerf.Hooks;

/// <summary>
/// The full-mode attribution helpers (plan 041), swapped by the Patch97 transpilers into
/// <c>Mission.SpawnAgent</c> (both <c>OnAgentBuild</c> calls) and <c>ManagedScriptHolder.TickComponents</c> (the
/// main-thread <c>ScriptComponentBehavior.OnTick</c> call and the four <c>TWParallel.For</c> blocks). Each makes
/// the engine call exactly once. Not attributing (no profiler, not measuring, probe mode, the mission's flag
/// off, off the main thread for a type table), it just calls; attributing, it times the call in try/finally, so
/// the engine call's exception propagates unchanged and is still recorded. A fault in the helper's own
/// bookkeeping turns attribution off for the process with one ERROR line. Thin (ADR-002).
/// </summary>
public static class MissionAttributionHooks
{
    /// <summary>Open delegate over the protected internal virtual <c>ScriptComponentBehavior.OnTick</c>; the
    /// <c>OnTick</c> swap is installed only when it bound, so it is never null where it is called.</summary>
    internal static Action<ScriptComponentBehavior, float>? ScriptTickCall;

    /// <summary>Set by a helper fault, for the process: no helper attributes or times by type any more, and
    /// later missions configure no attribution (<c>MissionTickProfilerHooks.ConfigureProbeMission</c>).</summary>
    internal static volatile bool Off;
    private static int _faultLogged;

    public static void TimedAgentBuild(MissionBehavior behavior, Agent agent, Banner banner)
    {
        var profiler = Attributing();
        if (profiler == null || !profiler.SpawnAttribution || Environment.CurrentManagedThreadId != profiler.MainThreadId)
        {
            behavior.OnAgentBuild(agent, banner);
            return;
        }
        var slot = Slot(profiler.SpawnBuilds, behavior.GetType(), "agent build");
        if (slot < 0)
        {
            behavior.OnAgentBuild(agent, banner);
            return;
        }
        var t0 = Stopwatch.GetTimestamp();
        try { behavior.OnAgentBuild(agent, banner); }
        finally { Record(profiler.SpawnBuilds, slot, t0, "agent build"); }
    }

    public static void TimedScriptTick(ScriptComponentBehavior component, float dt)
    {
        var call = ScriptTickCall!;
        var profiler = Attributing();
        if (profiler == null || !profiler.ScriptAttribution || HitchProbeHooks.ScriptOffMain
            || Environment.CurrentManagedThreadId != profiler.MainThreadId)
        {
            call(component, dt);
            return;
        }
        var slot = Slot(profiler.ScriptComponents, component.GetType(), "script attribution");
        if (slot < 0)
        {
            call(component, dt);
            return;
        }
        var t0 = Stopwatch.GetTimestamp();
        try { call(component, dt); }
        finally { Record(profiler.ScriptComponents, slot, t0, "script attribution"); }
    }

    public static void TimedParallelBlock(int fromInclusive, int toExclusive, float deltaTime,
        TWParallel.ParallelForWithDtAuxPredicate body, int grainSize) =>
        TimedBlock(fromInclusive, toExclusive, deltaTime, body, grainSize, occasional: false);

    public static void TimedOccasionalBlock(int fromInclusive, int toExclusive, float deltaTime,
        TWParallel.ParallelForWithDtAuxPredicate body, int grainSize) =>
        TimedBlock(fromInclusive, toExclusive, deltaTime, body, grainSize, occasional: true);

    // Any thread: the block totals are Interlocked in the profiler.
    private static void TimedBlock(int fromInclusive, int toExclusive, float deltaTime,
        TWParallel.ParallelForWithDtAuxPredicate body, int grainSize, bool occasional)
    {
        var profiler = Attributing();
        if (profiler == null)
        {
            TWParallel.For(fromInclusive, toExclusive, deltaTime, body, grainSize);
            return;
        }
        var t0 = Stopwatch.GetTimestamp();
        try { TWParallel.For(fromInclusive, toExclusive, deltaTime, body, grainSize); }
        finally
        {
            try
            {
                var elapsed = Stopwatch.GetTimestamp() - t0;
                if (occasional) profiler.AddOccasional(elapsed);
                else profiler.AddScriptParallel(elapsed);
            }
            catch (Exception ex) { Fault("parallel block", ex); }
        }
    }

    private static MissionTickProfiler? Attributing()
    {
        var profiler = MissionTickProfilerHooks.Profiler;
        return profiler != null && profiler.Measuring && profiler.BehaviorTiming && !Off ? profiler : null;
    }

    private static int Slot(BehaviorTickTable table, Type type, string part)
    {
        try { return table.SlotFor(type); }
        catch (Exception ex) { Fault(part, ex); return -1; }
    }

    private static void Record(BehaviorTickTable table, int slot, long t0, string part)
    {
        try { table.Record(slot, Stopwatch.GetTimestamp() - t0, 0); }
        catch (Exception ex) { Fault(part, ex); }
    }

    private static void Fault(string part, Exception ex)
    {
        Off = true;
        if (Interlocked.CompareExchange(ref _faultLogged, 1, 0) != 0)
            return;
        try { MissionTickProfilerHooks.Logger?.LogError(HitchProbeLines.BuildAttributionFault(part, ex)); }
        catch { /* diagnostic only: never throw into the engine */ }
    }

    /// <summary>Test-only: the fault latch back to its initial value. Never called from production code.</summary>
    internal static void ResetForTests()
    {
        Off = false;
        _faultLogged = 0;
    }
}
