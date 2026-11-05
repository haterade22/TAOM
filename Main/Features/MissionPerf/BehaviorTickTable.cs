using System;
using System.Collections.Generic;

namespace TAOM.Features.MissionPerf;

/// <summary>
/// Per-behaviour-type tick accumulators for the Patch97 tick profiler, in three layers: the OPEN
/// frame (written by <see cref="Record"/>), the WINDOW (the closed frames since the last
/// <c>[TickProfile]</c> line) and the MISSION (every closed frame since <see cref="ResetMission"/>,
/// for <c>[TickSummary]</c>). A call reaches the window and the mission only when its frame is folded,
/// so every total covers the same set of closed frames.
///
/// Each type gets a slot at its first sighting; each layer is one array of <see cref="Accum"/> indexed by
/// slot (initial capacity 128, doubling), so recording allocates only on growth or a first sighting.
/// Main thread only, NOT thread-safe: every caller is a mission-tick hook on the main thread.
/// </summary>
public sealed class BehaviorTickTable
{
    private const int InitialCapacity = 128;

    private readonly Dictionary<Type, int> _slots = new Dictionary<Type, int>(InitialCapacity);
    private int _count;
    private string[] _names = new string[InitialCapacity];

    private Accum[] _frame = new Accum[InitialCapacity];
    private Accum[] _window = new Accum[InitialCapacity];
    private Accum[] _mission = new Accum[InitialCapacity];
    private int[] _touched = new int[InitialCapacity];
    private int _touchedCount;
    private bool[] _picked = new bool[InitialCapacity];

    /// <summary>The slot for a behaviour type, created (with its cached <c>Type.Name</c>) at first sighting.</summary>
    public int SlotFor(Type type)
    {
        if (_slots.TryGetValue(type, out var slot))
            return slot;
        if (_count == _names.Length)
            Grow();
        slot = _count++;
        _names[slot] = type.Name;
        _slots.Add(type, slot);
        return slot;
    }

    /// <summary>One timed call into the open frame. A call can measure 0 ticks, so "touched" keys on calls.</summary>
    public void Record(int slot, long elapsedTicks, long allocBytes)
    {
        ref var f = ref _frame[slot];
        if (f.Calls == 0)
            _touched[_touchedCount++] = slot;
        f.Ticks += elapsedTicks;
        f.Calls++;
        if (elapsedTicks > f.MaxTicks)
            f.MaxTicks = elapsedTicks;
        f.Alloc += allocBytes;
    }

    /// <summary>Adds the open frame into the window and the mission, then clears the frame.</summary>
    public void FoldFrame()
    {
        for (var i = 0; i < _touchedCount; i++)
        {
            var s = _touched[i];
            _window[s].Add(in _frame[s]);
            _mission[s].Add(in _frame[s]);
        }
        ResetFrame();
    }

    /// <summary>Clears the open frame without folding it (the partial frame before a mission's first boundary).</summary>
    public void ResetFrame()
    {
        for (var i = 0; i < _touchedCount; i++)
            _frame[_touched[i]] = default;
        _touchedCount = 0;
    }

    public void ResetWindow() => Array.Clear(_window, 0, _count);

    public void ResetMission() => Array.Clear(_mission, 0, _count);

    /// <summary>The window's top <paramref name="n"/> by total time, ties by ordinal name. Allocates its result.</summary>
    public IReadOnlyList<BehaviorTotal> WindowTop(int n, long ticksPerSecond) => Top(n, ticksPerSecond, _window);

    /// <summary>The OPEN frame's top <paramref name="n"/>; used on hitch frames, before <see cref="FoldFrame"/>.</summary>
    public IReadOnlyList<BehaviorTotal> FrameTop(int n, long ticksPerSecond) => Top(n, ticksPerSecond, _frame);

    /// <summary>The mission's top <paramref name="n"/> over every folded frame since <see cref="ResetMission"/>.</summary>
    public IReadOnlyList<BehaviorTotal> MissionTop(int n, long ticksPerSecond) => Top(n, ticksPerSecond, _mission);

    // Selection, not a sort: n is at most 20 and the slot count about a hundred, and it needs no
    // scratch allocation beyond the result list.
    private IReadOnlyList<BehaviorTotal> Top(int n, long ticksPerSecond, Accum[] layer)
    {
        var result = new List<BehaviorTotal>(Math.Max(0, Math.Min(n, _count)));
        if (n <= 0 || ticksPerSecond <= 0)
            return result;
        Array.Clear(_picked, 0, _count);
        for (var k = 0; k < n; k++)
        {
            var best = -1;
            for (var s = 0; s < _count; s++)
            {
                if (_picked[s] || layer[s].Calls == 0)
                    continue;
                if (best < 0 || layer[s].Ticks > layer[best].Ticks
                    || (layer[s].Ticks == layer[best].Ticks && string.CompareOrdinal(_names[s], _names[best]) < 0))
                    best = s;
            }
            if (best < 0)
                break;
            _picked[best] = true;
            ref var b = ref layer[best];
            result.Add(new BehaviorTotal(_names[best], b.Ticks * 1000d / ticksPerSecond, b.Calls,
                b.MaxTicks * 1000d / ticksPerSecond, b.Alloc));
        }
        return result;
    }

    private void Grow()
    {
        var size = _names.Length * 2;
        Array.Resize(ref _names, size);
        Array.Resize(ref _frame, size);
        Array.Resize(ref _window, size);
        Array.Resize(ref _mission, size);
        Array.Resize(ref _touched, size);
        Array.Resize(ref _picked, size);
    }

    /// <summary>One behaviour type's ticks, calls, slowest call and allocation over one layer.</summary>
    private struct Accum
    {
        public long Ticks;
        public int Calls;
        public long MaxTicks;
        public long Alloc;

        public void Add(in Accum frame)
        {
            Ticks += frame.Ticks;
            Calls += frame.Calls;
            if (frame.MaxTicks > MaxTicks)
                MaxTicks = frame.MaxTicks;
            Alloc += frame.Alloc;
        }
    }
}

/// <summary>One behaviour type's totals over a frame, a window or a mission.</summary>
public sealed class BehaviorTotal
{
    public BehaviorTotal(string name, double ms, int calls, double maxMs, long allocBytes)
    {
        Name = name;
        Ms = ms;
        Calls = calls;
        MaxMs = maxMs;
        AllocBytes = allocBytes;
    }

    public string Name { get; }
    public double Ms { get; }
    public int Calls { get; }
    public double MaxMs { get; }
    public long AllocBytes { get; }
}
