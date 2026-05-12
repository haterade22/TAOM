using System;
using System.Diagnostics;
using TAOM.Core.Logging;

namespace TAOM.Features.EditorCacheRebuild.Progress;

/// <summary>
/// Helper for emitting cadenced progress lines with rate + ETA.
/// Thread-safe for the typical use case (Parallel.For incrementing through outer loop).
/// </summary>
public sealed class ProgressLogger
{
    private readonly IModLogger _logger;
    private readonly string _label;
    private readonly int _total;
    private readonly int _everyN;
    private readonly Stopwatch _sw;
    private readonly object _lock = new();
    private int _completed;
    private long _lastLogTicks;

    public ProgressLogger(IModLogger logger, string label, int total, int everyN = 50)
    {
        _logger = logger;
        _label = label;
        _total = total;
        _everyN = Math.Max(1, everyN);
        _sw = Stopwatch.StartNew();
        _lastLogTicks = 0;
    }

    public void Tick()
    {
        var done = System.Threading.Interlocked.Increment(ref _completed);
        if (done % _everyN != 0 && done != _total) return;

        lock (_lock)
        {
            var elapsed = _sw.Elapsed;
            if (done <= 0 || elapsed.TotalSeconds < 0.001) return;

            var pct = _total > 0 ? done * 100.0 / _total : 0;
            var rate = done / elapsed.TotalSeconds;
            var remaining = _total - done;
            var etaSeconds = rate > 0 ? remaining / rate : 0;

            _logger.LogInfo(
                $"[CacheRebuild] {_label} {done}/{_total} ({pct:F1}%) " +
                $"| elapsed {FormatDuration(elapsed)} " +
                $"| rate {rate:F2}/s " +
                $"| ETA {FormatDuration(TimeSpan.FromSeconds(etaSeconds))}");
            _lastLogTicks = elapsed.Ticks;
        }
    }

    public TimeSpan Elapsed => _sw.Elapsed;

    public static string FormatDuration(TimeSpan ts)
    {
        if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h {ts.Minutes}m {ts.Seconds}s";
        if (ts.TotalMinutes >= 1) return $"{(int)ts.TotalMinutes}m {ts.Seconds}s";
        return $"{ts.TotalSeconds:F1}s";
    }
}

public static class BannerLogger
{
    public static void LogBanner(IModLogger logger, string title)
    {
        const string Bar = "==============";
        logger.LogInfo($"[CacheRebuild] {Bar} {title} {Bar}");
    }
}
