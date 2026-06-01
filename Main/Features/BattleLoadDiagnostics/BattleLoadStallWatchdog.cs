using System;
using System.Threading;
using TAOM.Core.Logging;
using TAOM.Features.CrashReport;

namespace TAOM.Features.BattleLoadDiagnostics;

// Background-thread stall detector. A battle-load hang freezes the MAIN thread, so a
// main-thread timer can't time itself out — this uses a thread-pool Timer. When the
// loading window (opened at Mission.Initialize, closed at first OnMissionTick) has been
// open longer than the threshold, it:
//   1. writes a GUARANTEED "STILL LOADING" marker naming the last phase reached
//      (IModLogger's queue is thread-safe and flushed by its own background thread), then
//   2. best-effort triggers the CrashReport bundle so the user can ship the log in one
//      action.
// Some CrashReport collectors read live mission state; from this thread while the main
// thread is frozen they may return partial data — that's acceptable, the marker + the
// already-flushed phase log are the primary signal and the bundle is a bonus.
public sealed class BattleLoadStallWatchdog : IDisposable
{
    private const string Tag = "[BattleLoad]";
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private readonly IModLogger _logger;
    private readonly IBattleLoadDiagnosticsSettingsProvider _settings;
    private readonly IBattleLoadDiagnosticsService _service;
    private readonly ICrashReportService? _crashReport;

    private Timer? _timer;
    private long _lastWindowOpenedTicks = -1L;
    private bool _firedForCurrentWindow;

    public BattleLoadStallWatchdog(
        IModLogger logger,
        IBattleLoadDiagnosticsSettingsProvider settings,
        IBattleLoadDiagnosticsService service,
        ICrashReportService? crashReport = null)
    {
        _logger = logger;
        _settings = settings;
        _service = service;
        _crashReport = crashReport;
    }

    public void Start()
    {
        if (_timer != null) return;
        _timer = new Timer(_ => Poll(), null, PollInterval, PollInterval);
    }

    // Pure decision: fire once per window when it's been open at or past the threshold.
    public static bool ShouldFire(bool windowOpen, double elapsedSeconds, double thresholdSeconds, bool alreadyFired)
        => windowOpen && !alreadyFired && elapsedSeconds >= thresholdSeconds;

    private void Poll()
    {
        try
        {
            if (!_settings.IsEnabled || !_settings.StallWatchdogEnabled) return;

            var openedAt = BattleLoadLoadingWindow.OpenedAtUtc;
            if (!openedAt.HasValue)
            {
                _firedForCurrentWindow = false; // window closed — ready for the next load
                return;
            }

            // New window since we last fired? reset the latch so each load gets one fire.
            long openedTicks = openedAt.Value.Ticks;
            if (openedTicks != _lastWindowOpenedTicks)
            {
                _lastWindowOpenedTicks = openedTicks;
                _firedForCurrentWindow = false;
            }

            double elapsed = (DateTime.UtcNow - openedAt.Value).TotalSeconds;
            if (!ShouldFire(true, elapsed, _settings.StallWatchdogSeconds, _firedForCurrentWindow)) return;

            _firedForCurrentWindow = true;
            _logger.LogError($"{Tag} WATCHDOG STILL LOADING after {elapsed:F0}s — last {_service.CurrentStatusLine}");

            if (_settings.StallWatchdogBundleEnabled && _crashReport != null)
            {
                try
                {
                    var zip = _crashReport.HandleException(
                        new BattleLoadStallException($"Mission load stalled >{elapsed:F0}s; last {_service.CurrentStatusLine}"),
                        "BattleLoadStallWatchdog");
                    if (!string.IsNullOrEmpty(zip))
                        _logger.LogError($"{Tag} WATCHDOG bundle written: {zip}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"{Tag} WATCHDOG bundle failed: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            try { _logger.LogWarning($"{Tag} WATCHDOG poll failed: {ex.GetType().Name}: {ex.Message}"); }
            catch { /* never propagate from a timer callback */ }
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
    }
}
