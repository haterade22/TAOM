using System;
using System.Linq;
using TAOM.Features.CrashReport.Domain;

namespace TAOM.Features.CrashReport.Collectors;

// Captures the last N frame deltas (ms). Wired into Mission.Tick / ScreenManager.Tick
// by FrameTimingBufferHook so we can show "last frame was 1247ms — GC spike or hitch"
// in the crash report.
public sealed class FrameTimingBuffer
{
    private readonly RingBuffer<float> _frames;

    public FrameTimingBuffer(int capacity = 60)
    {
        _frames = new RingBuffer<float>(capacity);
    }

    public void Push(float frameTimeMs)
    {
        try { _frames.Push(frameTimeMs); } catch { }
    }

    public FrameTimingSnapshot Snapshot()
    {
        var samples = _frames.Snapshot();
        if (samples.Count == 0) return new FrameTimingSnapshot(Array.Empty<float>(), 0d, 0d, 0);
        double avg = samples.Average();
        double fps = avg > 0d ? 1000d / avg : 0d;
        return new FrameTimingSnapshot(samples, avg, fps, samples.Count);
    }

    public void Clear() => _frames.Clear();
}
