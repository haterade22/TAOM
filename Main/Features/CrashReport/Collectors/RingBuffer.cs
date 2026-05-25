using System.Collections.Generic;

namespace TAOM.Features.CrashReport.Collectors;

// Thread-safe fixed-capacity ring buffer. Old entries overwrite oldest when full.
// Snapshot() returns a stable copy in chronological order. Designed for the
// CampaignEvents and FrameTiming ring buffers — frequent writes, occasional read.
public sealed class RingBuffer<T>
{
    private readonly object _lock = new();
    private readonly T[] _items;
    private int _next;       // next write slot
    private int _count;

    public int Capacity => _items.Length;

    public RingBuffer(int capacity)
    {
        if (capacity <= 0) capacity = 1;
        _items = new T[capacity];
    }

    public void Push(T item)
    {
        lock (_lock)
        {
            _items[_next] = item;
            _next = (_next + 1) % _items.Length;
            if (_count < _items.Length) _count++;
        }
    }

    public IReadOnlyList<T> Snapshot()
    {
        lock (_lock)
        {
            var result = new List<T>(_count);
            // Oldest first.
            int start = _count < _items.Length ? 0 : _next;
            for (int i = 0; i < _count; i++)
                result.Add(_items[(start + i) % _items.Length]);
            return result;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _count = 0;
            _next = 0;
        }
    }
}
