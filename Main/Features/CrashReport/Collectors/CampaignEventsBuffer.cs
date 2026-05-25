using System;
using System.Collections.Generic;
using TAOM.Features.CrashReport.Domain;

namespace TAOM.Features.CrashReport.Collectors;

// Ring buffer for the last N CampaignEvents (capacity ~60 by default ≈ ~1 in-game minute
// of frequent ticks). The actual subscription to CampaignEvents.* lives in a behavior
// (CampaignEventsBufferBehavior) wired in SubModule.cs — this class is just the storage.
public sealed class CampaignEventsBuffer
{
    private readonly RingBuffer<CampaignEventEntry> _buffer;

    public CampaignEventsBuffer(int capacity = 60)
    {
        _buffer = new RingBuffer<CampaignEventEntry>(capacity);
    }

    public void Push(string eventName)
    {
        try
        {
            _buffer.Push(new CampaignEventEntry(
                CapturedAtUtc: DateTime.UtcNow.ToString("HH:mm:ss.fff"),
                EventName: eventName));
        }
        catch { /* never propagate from a diagnostic push */ }
    }

    public IReadOnlyList<CampaignEventEntry> Snapshot() => _buffer.Snapshot();
    public void Clear() => _buffer.Clear();
}
