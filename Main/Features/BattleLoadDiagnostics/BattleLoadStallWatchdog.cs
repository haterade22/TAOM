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

    // How long the engine's shader-compilation count may sit UNCHANGED before a load that is
    // "still compiling" stops counting as progress. A draining queue is a working load; a frozen
    // one is a wedge and still earns its bundle. 60 s is well clear of the slowest single compile
    // observed in b18f3441 (~0.7 s per shader program) without letting a real freeze hide.
    private const double ShaderNoProgressSeconds = 60d;

    // The CHURN BACKSTOP. "Changing" is not the same as "draining": a count that thrashes among
    // positive values (compiles completing while new requests arrive) refreshes the no-progress
    // clock forever without the load ever finishing, so a deferral gated only on ShaderNoProgress
    // Seconds would suppress the bundle for the entire session. That is the exact failure the
    // watchdog exists to prevent, inverted from a false positive into silence.
    //
    // TAOM has already been bitten by this in the same domain: ShaderPrecompileDecider carries a
    // named `ChurnTimeout` abort for "count > 0 CONTINUOUSLY, churns without ever settling", added
    // after the 1.4.7 precompile hang, because a per-frame-changing count slips straight past a
    // frozen-count guard. This is that lesson carried across.
    //
    // 15 minutes of CONTINUOUS COMPILATION, measured by the probe from the empty-to-non-empty edge
    // and reset on every dip to zero, NOT 15 minutes since the loading window opened. The first cut
    // used window time and a review pass caught it: shader compilation only starts at
    // Scene.ResumeLoadingRenderings, so on a big siege scene a legitimate 550 s of native scene
    // setup would have silently spent most of the allowance before the first shader compiled.
    private const double MaxContinuousCompileSeconds = 900d;

    private Timer? _timer;
    private long _lastWindowOpenedTicks = -1L;
    private bool _firedForCurrentWindow;
    private bool _deferralLoggedForCurrentWindow;

    // Set true by ShaderPrecompileRunner while a shader-precompile walk is active. The walk
    // intentionally loads battles that legitimately take many minutes (cold-cache item 1 = 3000
    // troops compiling every character shader — observed 830s on a slow machine), which would trip
    // the stall threshold and emit a SPURIOUS crash bundle. Volatile: written on the main thread,
    // read on this timer thread. (False-positive found in a user's cold run, 2026-06-18.)
    public static volatile bool SuppressStallDetection;

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

    /// <summary>What the watchdog should do once a window is past its stall threshold.</summary>
    public enum StallAction
    {
        /// <summary>The compile queue is draining. Hold off; this is a slow load, not a hang.</summary>
        Defer,

        /// <summary>Nothing compiling, the queue is frozen, or the probe never sampled. Fire.</summary>
        FireWedge,

        /// <summary>Still compiling and still changing, but past the continuous-compile cap. Fire.</summary>
        FireChurnCapped,
    }

    // ONE decision with three outcomes. It used to be a bool predicate plus a separate formatter
    // that re-derived the same three-term algebra to decide whether to print `churn-capped`; a
    // review pass flagged that as the "two seams that gate the same decision must carry the SAME
    // guards" trap, so the token now DERIVES from this verdict and the two cannot disagree.
    //
    // Bundle b18f3441 (2026-09-04) is the case Defer exists for: 290 s past
    // FinishMissionLoadingDone with 818 compile_shader lines inside the window and the first
    // Mission.Tick held behind SceneView.ReadyToRender().
    //
    // Both degenerate readings deliberately fire rather than defer:
    //   shadersInFlight == -1  the probe never sampled (hook never ran, or the native read threw).
    //                          Absent evidence must not buy a deferral, or a binding failure would
    //                          silently disable the watchdog. Same rule as `polls=0`.
    //   shadersInFlight == 0   nothing is compiling, so a still-open window is a real wedge.
    public static StallAction Decide(
        int shadersInFlight,
        double secondsSinceCountChanged,
        double secondsCompilingContinuously,
        double noProgressSeconds,
        double maxContinuousCompileSeconds)
    {
        if (shadersInFlight <= 0) return StallAction.FireWedge;
        if (!(secondsSinceCountChanged < noProgressSeconds)) return StallAction.FireWedge;
        return secondsCompilingContinuously >= maxContinuousCompileSeconds
            ? StallAction.FireChurnCapped
            : StallAction.Defer;
    }

    private void Poll()
    {
        try
        {
            // A shader-precompile walk intentionally does multi-minute loads — never flag those as stalls.
            if (SuppressStallDetection) { _firedForCurrentWindow = false; _deferralLoggedForCurrentWindow = false; return; }
            if (!_settings.IsEnabled || !_settings.StallWatchdogEnabled) return;

            var openedAt = BattleLoadLoadingWindow.OpenedAtUtc;
            if (!openedAt.HasValue)
            {
                _firedForCurrentWindow = false; // window closed — ready for the next load
                _deferralLoggedForCurrentWindow = false;
                return;
            }

            // New window since we last fired? reset the latch so each load gets one fire.
            long openedTicks = openedAt.Value.Ticks;
            if (openedTicks != _lastWindowOpenedTicks)
            {
                _lastWindowOpenedTicks = openedTicks;
                _firedForCurrentWindow = false;
                _deferralLoggedForCurrentWindow = false;
            }

            var now = DateTime.UtcNow;
            double elapsed = (now - openedAt.Value).TotalSeconds;
            if (!ShouldFire(true, elapsed, _settings.StallWatchdogSeconds, _firedForCurrentWindow)) return;

            // A load can be past the threshold and still WORKING: on a cold shader cache the engine
            // holds the first Mission.Tick behind SceneView.ReadyToRender() while it compiles.
            // Deferring must NOT set _firedForCurrentWindow — if the queue later freezes, this same
            // window still owes a bundle.
            int shaders = BattleLoadRenderWaitProbe.ShadersInFlight;
            double sinceChange = BattleLoadRenderWaitProbe.SecondsSinceLastChange(now) ?? double.MaxValue;
            double compiling = BattleLoadRenderWaitProbe.SecondsCompilingContinuously(now) ?? 0d;
            var action = Decide(shaders, sinceChange, compiling, ShaderNoProgressSeconds, MaxContinuousCompileSeconds);

            if (action == StallAction.Defer)
            {
                // Deferring must NOT set _firedForCurrentWindow: if the queue later freezes or the
                // continuous-compile cap trips, this same window still owes a bundle.
                if (_deferralLoggedForCurrentWindow) return;
                _deferralLoggedForCurrentWindow = true;
                _logger.LogWarning(
                    $"{Tag} WATCHDOG DEFERRED after {elapsed:F0}s — shader compilation still in progress " +
                    $"(shaders={shaders}, compiling {compiling:F0}s); not a hang. Capped at " +
                    $"{MaxContinuousCompileSeconds:F0}s of unbroken compilation. Last {_service.CurrentStatusLine}");
                return;
            }

            _firedForCurrentWindow = true;
            _logger.LogError(
                $"{Tag} WATCHDOG STILL LOADING after {elapsed:F0}s — {FormatShaderToken(shaders)}" +
                $"{FormatChurnToken(action)}last {_service.CurrentStatusLine}");

            if (_settings.StallWatchdogBundleEnabled && _crashReport != null)
            {
                try
                {
                    var zip = _crashReport.HandleException(
                        new BattleLoadStallException(
                            $"Mission load stalled >{elapsed:F0}s; {FormatShaderToken(shaders)}" +
                            $"{FormatChurnToken(action)}last {_service.CurrentStatusLine}"),
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

    // The shader reading a fired bundle was taken with. Omitted when the probe never sampled — an
    // unmeasured value is absent, never zero, because `shaders=0` reads as a real engine answer.
    internal static string FormatShaderToken(int shadersInFlight) =>
        shadersInFlight >= 0 ? $"shaders={shadersInFlight} " : string.Empty;

    // Says WHY a bundle fired while the compile queue was still moving: the churn backstop, not a
    // frozen queue. Without it the two cases are indistinguishable in the artifact, and they call
    // for opposite responses (a capped churn is "this machine needs a warm cache"; a frozen count
    // is "the compiler is stuck on one shader").
    // Derived from the verdict, never re-derived from the inputs, so it cannot contradict the
    // decision that produced it.
    internal static string FormatChurnToken(StallAction action) =>
        action == StallAction.FireChurnCapped ? "churn-capped " : string.Empty;

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
    }
}
