// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), frame-buffer-guard and frame-buffer-watch.
using System;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.SkeletonBuffer;

/// <summary>
/// Reads the engine's two per-frame skeleton pools' fill during a mission, keeps each pool's peak, and writes one
/// <c>[SkeletonBuffer]</c> line when the mission ends, with each pool's guard overflow count beside its peak
/// (docs/features/skeleton-buffer-guard.md). When nothing guards pool 1 it also shows one on-screen warning at 90 percent,
/// because then the battle can freeze for good. Reads go through
/// <see cref="ISkeletonBufferMemoryAdapter"/>, which fails instead of crashing. One mission at a time: the state is
/// replaced by <see cref="Begin"/>. Everything public is called from the main thread (the mission behavior) and never throws.
/// </summary>
public sealed class SkeletonBufferWatchService
{
    /// <summary>Pool 1's two buffers sit at [global] + 0x9D0, 0x128 bytes apart; the first int of each is its fill counter.</summary>
    internal const long BufferOffset = 0x9D0;
    internal const long BufferStride = 0x128;

    /// <summary>The second pool's two buffers follow at [global] + 0xC28, the same stride apart, with the same first-int fill counter.</summary>
    internal const long Pool2Offset = 0xC28;

    private readonly ISkeletonBufferGuardService _guard;
    private readonly ISkeletonBufferMemoryAdapter _memory;
    private readonly ISkeletonBufferEngineAdapter _engine;
    private readonly ISkeletonBufferSettingsProvider _settings;
    private readonly IDedicatedServerProvider _server;
    private readonly IModLogger _logger;

    private SkeletonBufferWatchState? _state;
    private SkeletonBufferTarget? _target;
    private int _overflowBaseline;
    private bool _baselineKnown;
    private int _pool2OverflowBaseline;
    private bool _pool2BaselineKnown;
    private bool _faulted;

    public SkeletonBufferWatchService(ISkeletonBufferGuardService guard, ISkeletonBufferMemoryAdapter memory,
        ISkeletonBufferEngineAdapter engine, ISkeletonBufferSettingsProvider settings, IDedicatedServerProvider server,
        IModLogger logger)
    {
        _guard = guard;
        _memory = memory;
        _engine = engine;
        _settings = settings;
        _server = server;
        _logger = logger;
    }

    /// <summary>Starts a mission's watch, or does nothing when the feature has no target or the setting is off.</summary>
    public void Begin()
    {
        _state = null;
        _target = null;
        _faulted = false;
        _baselineKnown = false;
        _pool2BaselineKnown = false;
        try
        {
            var target = _guard.Target;
            if (target == null) return;   // the install line already said why
            if (!_settings.WatchEnabled)
            {
                _logger.LogInfo(SkeletonBufferLines.WatchOff());
                return;
            }

            _target = target;
            _state = new SkeletonBufferWatchState();
            if (target.Pool1CounterAddress != 0)
            {
                var counter = _memory.ReadInt32(target.Pool1CounterAddress);
                _baselineKnown = counter.HasValue;
                _overflowBaseline = counter ?? 0;
            }
            if (target.Pool2CounterAddress != 0)
            {
                var counter = _memory.ReadInt32(target.Pool2CounterAddress);
                _pool2BaselineKnown = counter.HasValue;
                _pool2OverflowBaseline = counter ?? 0;
            }
        }
        catch (Exception ex)
        {
            _state = null;   // the watch is not running for this mission, and the line says so
            _target = null;
            Fault(SkeletonBufferLines.WatchStartFault(ex.GetType().Name, ex.Message));
        }
    }

    /// <summary>One reading. <paramref name="agentCount"/> is asked only when the reading is a new peak.</summary>
    public void Tick(Func<int> agentCount, double secondsIntoMission)
    {
        var state = _state;
        var target = _target;
        if (state == null || target == null) return;
        try
        {
            var pointer = _memory.ReadInt64(target.GlobalAddress);
            if (pointer == null || pointer.Value == 0) return;

            var fill = ReadPool(pointer.Value, BufferOffset);
            if (fill != null)
            {
                state.Observe(fill.Value, secondsIntoMission, agentCount);
                // The on-screen warning is pool 1's alone: pool 2 reaching 90 percent is a log note.
                if (!_server.IsDedicatedServer && state.TakeWarning(target.Pool1Guarded))
                    _engine.ShowWarning(Math.Min(100, (int)state.PeakPercent));
            }

            if (target.WatchPool2)
            {
                var fill2 = ReadPool(pointer.Value, Pool2Offset);
                if (fill2 != null) state.ObservePool2(fill2.Value);
            }
        }
        catch (Exception ex)
        {
            Fault(SkeletonBufferLines.WatchReadFault(ex.GetType().Name, ex.Message));
        }
    }

    /// <summary>Ends the mission: one peak line, once. Called from OnEndMission and from OnRemoveBehavior.</summary>
    public void End()
    {
        var state = _state;
        var target = _target;
        _state = null;
        _target = null;
        if (state == null || target == null) return;
        try
        {
            if (!state.HasSamples)
            {
                _logger.LogInfo(SkeletonBufferLines.NoReadings);
                return;
            }

            var overflows = OverflowsThisMission(target.Pool1CounterAddress, _baselineKnown, _overflowBaseline);
            var pool2Overflows = target.WatchPool2
                ? OverflowsThisMission(target.Pool2CounterAddress, _pool2BaselineKnown, _pool2OverflowBaseline)
                : null;
            var pool2Note = target.WatchPool2 ? GuardNote(target.Pool2Guarded, target.Pool2CounterAddress, pool2Overflows) : null;
            var line = SkeletonBufferLines.Peak(state, GuardNote(target.Pool1Guarded, target.Pool1CounterAddress, overflows), pool2Note);
            // A pool at 90 percent is a warning only while nothing guards it; a guarded pool's fill is a log note.
            var unguardedNearFull = (state.Pool1OverNinety && !target.Pool1Guarded) || (state.Pool2OverNinety && !target.Pool2Guarded);
            if (overflows > 0 || pool2Overflows > 0 || unguardedNearFull) _logger.LogWarning(line);
            else _logger.LogInfo(line);
        }
        catch (Exception ex)
        {
            Fault(SkeletonBufferLines.WatchEndFault(ex.GetType().Name, ex.Message));
        }
    }

    /// <summary>One pool's fill: the larger of its two buffers' counters, or whichever one can be read; null when neither can.</summary>
    private int? ReadPool(long pointer, long offset)
    {
        var first = _memory.ReadInt32(pointer + offset);
        var second = _memory.ReadInt32(pointer + offset + BufferStride);
        if (first == null) return second;
        if (second == null) return first;
        return Math.Max(first.Value, second.Value);
    }

    /// <summary>A counter's growth since <see cref="Begin"/>; null when TAOM has no counter there or it cannot be read.</summary>
    private int? OverflowsThisMission(long counterAddress, bool baselineKnown, int baseline)
    {
        if (counterAddress == 0 || !baselineKnown) return null;
        var now = _memory.ReadInt32(counterAddress);
        return now.HasValue ? now.Value - baseline : (int?)null;
    }

    private static string GuardNote(bool guarded, long counterAddress, int? overflows)
    {
        if (counterAddress != 0)
            return overflows.HasValue ? SkeletonBufferLines.GuardOverflows(overflows.Value) : SkeletonBufferLines.OverflowUnreadable;
        return guarded ? SkeletonBufferLines.ForeignGuard : SkeletonBufferLines.NoGuard;
    }

    /// <summary>One warning per mission, whichever step failed first.</summary>
    private void Fault(string line)
    {
        if (_faulted) return;
        _faulted = true;
        try { _logger.LogWarning(line); }
        catch { /* diagnostic only */ }
    }
}
