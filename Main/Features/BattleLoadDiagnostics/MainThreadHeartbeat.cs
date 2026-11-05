using System;
using System.Threading;

namespace TAOM.Features.BattleLoadDiagnostics;

/// <summary>
/// The main thread's liveness stamp. <c>SubModule.OnApplicationTick</c> beats once per managed frame,
/// which every native frame reaches through both engine entries (<c>Managed.ApplicationTick</c> and
/// <c>ApplicationTickLight</c>, v1.4.8), and <see cref="MainThreadFreezeWatchdog"/> reads it from a timer
/// thread. One volatile write per frame: nothing here may log or allocate.
/// </summary>
public sealed class MainThreadHeartbeat
{
    public static readonly MainThreadHeartbeat Instance = new MainThreadHeartbeat();

    private long _lastBeatUtcTicks; // 0 = never
    private Thread? _thread;

    public void Beat() => BeatAt(DateTime.UtcNow.Ticks);

    internal void BeatAt(long utcTicks) => Volatile.Write(ref _lastBeatUtcTicks, utcTicks);

    public void BindCurrentThread() => Volatile.Write(ref _thread, Thread.CurrentThread);

    public bool TryRead(out long lastBeatUtcTicks, out Thread? thread)
    {
        lastBeatUtcTicks = Volatile.Read(ref _lastBeatUtcTicks);
        thread = Volatile.Read(ref _thread);
        return lastBeatUtcTicks != 0L;
    }
}
