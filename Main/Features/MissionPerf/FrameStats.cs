using System;
using System.Collections.Generic;

namespace TAOM.Features.MissionPerf;

/// <summary>
/// A window of frame times that closes every <c>intervalSeconds</c> of wall clock. Wall clock,
/// not mission time: mission time slows under time scaling and stops in the order menu, and the
/// question the <c>[MissionPerf]</c> line answers is how long frames take.
///
/// Count, average and max cover every frame recorded. The percentile set is bounded at
/// <c>maxSamples</c> newest frames so a stall early in the window cannot pin it and the buffer
/// cannot grow without limit at very high frame rates; at 5 s windows the default cap only bites
/// above 819 fps. Pure by construction (no clock, no engine), so it is tested numerically.
/// </summary>
public sealed class FrameStats
{
    private readonly double _intervalSeconds;
    private readonly int _maxSamples;
    private readonly List<double> _samples;
    private double[] _sorted = new double[0];
    private int _sampleHead;
    private int _frames;
    private double _sumMs;
    private double _maxMs;
    private double _windowStart;
    private bool _clockSet;

    public FrameStats(double intervalSeconds = 5.0, int maxSamples = 4096)
    {
        _intervalSeconds = intervalSeconds > 0 ? intervalSeconds : 5.0;
        _maxSamples = maxSamples > 0 ? maxSamples : 4096;
        _samples = new List<double>(Math.Min(_maxSamples, 1024));
    }

    public void Record(double frameMs)
    {
        if (double.IsNaN(frameMs) || double.IsInfinity(frameMs) || frameMs < 0)
            return;
        _frames++;
        _sumMs += frameMs;
        if (frameMs > _maxMs)
            _maxMs = frameMs;
        if (_samples.Count < _maxSamples)
        {
            _samples.Add(frameMs);
            return;
        }
        // Ring: overwrite the oldest so the set always holds the newest samples.
        _samples[_sampleHead] = frameMs;
        _sampleHead = (_sampleHead + 1) % _maxSamples;
    }

    /// <summary>True once a full interval has elapsed since the window opened. The first call
    /// opens the window, so the first line lands one interval into the mission.</summary>
    public bool ShouldEmit(double nowSeconds)
    {
        if (!_clockSet)
        {
            _clockSet = true;
            _windowStart = nowSeconds;
            return false;
        }
        return nowSeconds - _windowStart >= _intervalSeconds;
    }

    /// <summary>Closes the window: reports it and opens the next one at <paramref name="nowSeconds"/>.</summary>
    public FrameWindow Emit(double nowSeconds)
    {
        var seconds = _clockSet ? nowSeconds - _windowStart : 0.0;
        var average = _frames > 0 ? _sumMs / _frames : 0.0;
        var fps = seconds > 0.0 ? _frames / seconds : 0.0;
        var window = new FrameWindow(_frames, seconds, average, Percentile95(), _maxMs, fps);

        _frames = 0;
        _sumMs = 0.0;
        _maxMs = 0.0;
        _samples.Clear();
        _sampleHead = 0;
        _windowStart = nowSeconds;
        _clockSet = true;
        return window;
    }

    /// <summary>Forgets the clock as well as the window, for a new mission.</summary>
    public void Reset()
    {
        Emit(0.0);
        _clockSet = false;
    }

    // Nearest-rank percentile over a sorted copy: at most maxSamples doubles once per window.
    private double Percentile95()
    {
        var count = _samples.Count;
        if (count == 0)
            return 0.0;
        if (_sorted.Length < count)
            _sorted = new double[count];
        _samples.CopyTo(_sorted, 0);
        Array.Sort(_sorted, 0, count);
        var rank = (int)Math.Ceiling(0.95 * count);
        return _sorted[Math.Max(0, Math.Min(count - 1, rank - 1))];
    }
}

/// <summary>One closed window of frame times.</summary>
public readonly struct FrameWindow
{
    public FrameWindow(int frames, double seconds, double averageMs, double p95Ms, double maxMs)
        : this(frames, seconds, averageMs, p95Ms, maxMs, seconds > 0.0 ? frames / seconds : 0.0)
    {
    }

    internal FrameWindow(int frames, double seconds, double averageMs, double p95Ms, double maxMs, double fps)
    {
        Frames = frames;
        Seconds = seconds;
        AverageMs = averageMs;
        P95Ms = p95Ms;
        MaxMs = maxMs;
        Fps = fps;
    }

    public int Frames { get; }
    public double Seconds { get; }
    public double AverageMs { get; }
    public double P95Ms { get; }
    public double MaxMs { get; }
    public double Fps { get; }
}
