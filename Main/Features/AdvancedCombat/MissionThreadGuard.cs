using System;
using System.Collections.Concurrent;
using System.Threading;

namespace TAOM.Features.AdvancedCombat;

/// <summary>
/// Which thread is the mission's main thread, and a once-per-site report when TAOM touches the
/// engine from another one. Single-player ticks agents on an asynchronous AI thread
/// (<c>MissionState.cs:201</c> passes <c>asyncAITick: true</c>; <c>Mission.TickAgentsAndTeams</c> is
/// the native callback that runs <c>Agent.Tick</c> there, v1.4.8). TAOM's creature trees used to run
/// inside that tick and register blows from it, racing the main thread's agent-removed callbacks over
/// the tree logic's collections (#592). The trees now tick from the main-thread mission tick; this
/// guard is how a player's log proves it, and how a regression announces itself instead of hanging.
/// Pure: thread ids and a set of reported sites.
/// </summary>
public static class MissionThreadGuard
{
    private const int Unmarked = -1;
    private static volatile int _mainThreadId = Unmarked;
    private static long _offThreadCalls;
    private static readonly ConcurrentDictionary<string, byte> _reportedSites = new();

    /// <summary>Record the calling thread as the main mission thread. Call from a mission tick.</summary>
    public static void MarkMainThread() => _mainThreadId = Thread.CurrentThread.ManagedThreadId;

    /// <summary>Calls seen off the marked thread since the last reset.</summary>
    public static long OffThreadCalls => Interlocked.Read(ref _offThreadCalls);

    /// <summary>
    /// True on the marked main thread, and before any mark: no asynchronous agent tick runs before
    /// the first mission tick, so an unmarked guard can only be asked from the main thread.
    /// </summary>
    public static bool IsOnMainThread
    {
        get
        {
            int main = _mainThreadId;
            return main == Unmarked || Thread.CurrentThread.ManagedThreadId == main;
        }
    }

    /// <summary>
    /// True when the caller is not on the marked main thread. Reports once per <paramref name="site"/>
    /// through <paramref name="report"/>; later off-thread calls from the same site are counted only.
    /// Before any mark nothing is off-thread, because there is nothing to compare against.
    /// </summary>
    public static bool NoteCall(string site, Action<string> report)
    {
        int main = _mainThreadId;
        int current = Thread.CurrentThread.ManagedThreadId;
        if (main == Unmarked || current == main) return false;

        Interlocked.Increment(ref _offThreadCalls);
        if (_reportedSites.TryAdd(site, 0))
            report?.Invoke($"[TAOM] {site} ran off the main mission thread (thread {current}, main {main}): " +
                           "a creature tree, blow, grid read or engine callback is on the engine's asynchronous " +
                           "agent tick (#592, #595).");
        return true;
    }

    public static void ResetForTests()
    {
        _mainThreadId = Unmarked;
        Interlocked.Exchange(ref _offThreadCalls, 0);
        _reportedSites.Clear();
    }
}
