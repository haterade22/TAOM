using System;
using TAOM.Core.Logging;

namespace TAOM.Features.MissionPerf.AnimMemory;

/// <summary>
/// One mission of the clip memory probe. It samples the engine's on-demand clip byte total once a
/// second and writes one <c>[AnimMem]</c> line every 5 s, aggregating the window (last value, min,
/// max, drops, samples with a clip loading) so no one-second sample is lost; at mission end it writes
/// one more such line for a partial window, then a summary over the whole mission. A drop is a sample
/// lower than the one before it, whichever window that was in: a sign that an eviction pass ran in
/// between, not a count of evictions (a pass and a reload of the same bytes inside one second leave
/// none). Each sample, and so each <c>loadingNow</c>, is a single point in time. Times are seconds since
/// <see cref="Start"/>. Any exception stops the session for the mission with one ERROR line.
/// </summary>
internal sealed class AnimMemorySession
{
    private const double SampleSeconds = 1.0;
    private const double LineSeconds = 5.0;

    private readonly IAnimClipMemoryProbe _probe;
    private readonly IModLogger _logger;

    private bool _stopped;
    private bool _errorLogged;
    private double _nextSample;
    private double _nextLine;

    private int _previous;
    private bool _lastLoading;

    private int _windowSamples;
    private int _windowDrops;
    private int _windowLoading;
    private int _windowMin;
    private int _windowMax;

    private int _samples;
    private int _start;
    private int _peak;
    private int _atOrAbove90;
    private int _drops;
    private int _loading;

    internal AnimMemorySession(IAnimClipMemoryProbe probe, IModLogger logger)
    {
        _probe = probe;
        _logger = logger;
    }

    internal void Start(double now)
    {
        try
        {
            if (!TakeSample())
                return;
            _logger.LogInfo(AnimMemLine.MissionStart(_previous, _probe.BudgetBytes, _lastLoading));
            _nextSample = now + SampleSeconds;
            _nextLine = now + LineSeconds;
        }
        catch (Exception ex)
        {
            Stop(ex);
        }
    }

    internal void Tick(double now)
    {
        if (_stopped)
            return;
        try
        {
            if (now < _nextSample)
                return;
            _nextSample = now + SampleSeconds;
            if (!TakeSample() || now < _nextLine)
                return;

            _logger.LogInfo(AnimMemLine.Periodic(now, _previous, _probe.BudgetBytes, _lastLoading, _windowDrops,
                _windowMin, _windowMax, _windowLoading, _windowSamples));
            _nextLine = now + LineSeconds;
            _windowSamples = 0;
            _windowDrops = 0;
            _windowLoading = 0;
        }
        catch (Exception ex)
        {
            Stop(ex);
        }
    }

    internal void End(double now)
    {
        try
        {
            if (_samples == 0)
                return;
            // The window since the last 5 s line would otherwise reach the log only through the
            // mission-wide totals, and its min and max nowhere.
            if (_windowSamples > 0)
                _logger.LogInfo(AnimMemLine.Periodic(now, _previous, _probe.BudgetBytes, _lastLoading, _windowDrops,
                    _windowMin, _windowMax, _windowLoading, _windowSamples));
            _logger.LogInfo(AnimMemLine.Summary(now, _samples, _start, _previous, _peak, _probe.BudgetBytes,
                _atOrAbove90, _drops, _loading, _stopped));
        }
        catch (Exception ex)
        {
            Stop(ex);
        }
    }

    /// <summary>Reads one sample and records it; false when the read was not a usable byte count.</summary>
    private bool TakeSample()
    {
        var bytes = _probe.ReadLoadedBytes();
        if (bytes < 0)
        {
            // The probe has already turned itself off for the process and logged why.
            _stopped = true;
            return false;
        }
        var loading = _probe.IsAnyClipLoading();

        var drop = _samples > 0 && bytes < _previous;
        if (_samples == 0)
            _start = bytes;
        _samples++;
        if (bytes > _peak)
            _peak = bytes;
        if (bytes * 10L >= _probe.BudgetBytes * 9L)
            _atOrAbove90++;
        if (drop)
            _drops++;
        if (loading)
            _loading++;

        if (_windowSamples == 0 || bytes < _windowMin)
            _windowMin = bytes;
        if (_windowSamples == 0 || bytes > _windowMax)
            _windowMax = bytes;
        _windowSamples++;
        if (drop)
            _windowDrops++;
        if (loading)
            _windowLoading++;

        _previous = bytes;
        _lastLoading = loading;
        return true;
    }

    private void Stop(Exception ex)
    {
        _stopped = true;
        if (_errorLogged)
            return;
        _errorLogged = true;
        try { _logger.LogError(AnimMemLine.Stopped(ex)); }
        catch { /* diagnostic only */ }
    }
}
