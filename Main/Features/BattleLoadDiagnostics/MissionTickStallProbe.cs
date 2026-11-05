using System;
using System.Threading;

namespace TAOM.Features.BattleLoadDiagnostics;

/// <summary>
/// Marks one engine tick as in flight: the thread running it and when it started. Patch91 brackets
/// <c>Mission.TickAgentsAndTeamsImp</c> (the asynchronous agent tick) and
/// <c>MissionState.TickMissionAux</c> (the main thread's whole mission frame: the native
/// <c>Mission.Tick</c>, whose callbacks include <c>OnPreTick</c> and every native-raised agent callback,
/// then the managed <c>Mission.OnTick</c>) with <see cref="Enter"/> and <see cref="Exit"/>, and
/// <see cref="MissionTickStallWatchdog"/> reads them from a timer thread. The next frame's
/// <c>Mission.OnPreTick</c> spins in <c>WaitTickCompletion</c> until the agent tick finishes, so an
/// agent tick that never exits is a frozen game with nothing in the log (#634). Pure: no engine types.
/// </summary>
public sealed class MissionTickStallProbe
{
    /// <summary><c>Mission.TickAgentsAndTeamsImp</c>, on the asynchronous agent thread in single-player.</summary>
    public static readonly MissionTickStallProbe AsyncAgentTick = new("async agent tick");

    /// <summary><c>MissionState.TickMissionAux</c> while the mission is <c>Continuing</c>, on the main thread.</summary>
    public static readonly MissionTickStallProbe MissionFrame = new("mission frame");

    private Thread? _thread;
    private long _sinceUtcTicks;

    public MissionTickStallProbe(string name) => Name = name;

    public string Name { get; }

    public void Enter() => EnterAt(DateTime.UtcNow.Ticks);

    // The thread goes in before the timestamp, so a reader that sees the timestamp sees its thread.
    internal void EnterAt(long utcTicks)
    {
        Volatile.Write(ref _thread, Thread.CurrentThread);
        Volatile.Write(ref _sinceUtcTicks, utcTicks);
    }

    public void Exit() => Volatile.Write(ref _sinceUtcTicks, 0L);

    /// <summary>True while a tick is in flight, with when it started and the thread running it.</summary>
    public bool TryRead(out long sinceUtcTicks, out Thread? thread)
    {
        sinceUtcTicks = Volatile.Read(ref _sinceUtcTicks);
        thread = Volatile.Read(ref _thread);
        return sinceUtcTicks != 0L;
    }
}
