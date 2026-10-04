using System;
using System.Threading;

namespace TAOM.Features.MissionPerf.Hooks;

/// <summary>
/// The probe's own diagnostics: the ERROR line a faulted part writes, and the guard for a finalizer that runs
/// with no prefix before it. PatchShield's rescue strips a shielded owner's prefixes, postfixes and transpilers
/// after a missing-API exception and never its finalizers (and TAOM's owner <c>com.taom.mod</c> is not
/// protected), and a prefix before ours that throws keeps ours from running, so an exit can run alone.
///
/// Each call carries its own <see cref="ProbeState"/> from its enter to its exit (Harmony's <c>__state</c>: one
/// local per call, started at 0, so it says nothing of any other call, thread or nesting level). Every enter sets
/// <see cref="ProbeState.Entered"/> first, whatever the profiler is doing (a not-measuring or faulted-off enter still
/// pairs with its exit), and an exit takes it through <see cref="Pair"/>: its own pair closes the call, a rerun of
/// that exit (Harmony reruns every finalizer when a later one throws) finds it closed and does nothing, and one
/// that finds <see cref="ProbeState.PrefixMissing"/> records nothing, is counted, and writes one WARNING line per
/// method per process. The counts are written once per measured mission's end (<see cref="TakePrefixMissingCalls"/>).
/// </summary>
public static partial class HitchProbeHooks
{
    // Last bracket is Spawn: the arrays below are indexed by ProbeBracket.
    private const int BracketCount = (int)ProbeBracket.Spawn + 1;

    // Finalizer calls that had no prefix before them, per bracket (every one, the first included), and the
    // once-per-process latches for each bracket's missing-prefix line.
    private static readonly int[] _prefixMissingCalls = new int[BracketCount];
    private static readonly int[] _prefixMissingLogged = new int[BracketCount];

    /// <summary>One ERROR line naming the part that turned itself off; never throws.</summary>
    internal static void HookFault(string part, Exception ex)
    {
        try { MissionTickProfilerHooks.Logger?.LogError(HitchProbeLines.BuildHookFault(part, ex)); }
        catch { /* diagnostic only: never throw into the engine */ }
    }

    /// <summary>
    /// The first thing an exit does. True when this call's prefix ran and nothing has closed the call yet (the
    /// state becomes <see cref="ProbeState.Closed"/>), so the caller goes on to record. False, so the caller
    /// records nothing, for a call already closed (a rerun) and for a call whose prefix did not run: that one is
    /// counted, and its first occurrence in the process writes <paramref name="line"/> as one WARNING. The latch is
    /// set before the write, so a logger that throws is tried once. Never throws into the engine.
    /// </summary>
    private static bool Pair(ref ProbeState state, ProbeBracket bracket, string line)
    {
        if (state == ProbeState.Entered)
        {
            state = ProbeState.Closed;
            return true;
        }
        if (state == ProbeState.PrefixMissing)
            CountMissingPrefix(bracket, line);
        return false;
    }

    private static void CountMissingPrefix(ProbeBracket bracket, string line)
    {
        var index = (int)bracket;
        Interlocked.Increment(ref _prefixMissingCalls[index]);
        if (Interlocked.CompareExchange(ref _prefixMissingLogged[index], 1, 0) != 0)
            return;
        try { MissionTickProfilerHooks.Logger?.LogWarning(line); }
        catch { /* diagnostic only: never throw into the engine */ }
    }

    /// <summary>
    /// The finalizer calls that had no prefix before them since the last take, per bracket in
    /// <see cref="ProbeBracket"/> order, each count reset to 0 as it is read; null when there were none.
    /// </summary>
    internal static int[]? TakePrefixMissingCalls()
    {
        int[]? taken = null;
        for (var i = 0; i < _prefixMissingCalls.Length; i++)
        {
            var calls = Interlocked.Exchange(ref _prefixMissingCalls[i], 0);
            if (calls == 0)
                continue;
            taken ??= new int[_prefixMissingCalls.Length];
            taken[i] = calls;
        }
        return taken;
    }

    private static void ResetDiagnosticsForTests()
    {
        Array.Clear(_prefixMissingCalls, 0, _prefixMissingCalls.Length);
        Array.Clear(_prefixMissingLogged, 0, _prefixMissingLogged.Length);
    }
}
