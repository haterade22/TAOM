using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using TAOM.Core.Logging;

namespace TAOM.Features.BattleLoadDiagnostics;

// Exit-stall stack sampler (#331 round 2). The ~107s tournament-exit stall froze the MAIN
// thread inside Mission.EndMissionInternal, so — like BattleLoadStallWatchdog — only a
// background thread can observe it. The sampler is armed by LogExitBegin and DISARMED at
// MapResumed (#425): teardown is what it watches, and everything after MapResumed is player
// time — menus, conversations, loot — field-measured at 123 s with three [ERROR] samples
// fired into an ordinary quartermaster chat before the disarm existed. The logging window
// itself stays open to FirstMapTick (or ResetLifecycle/MissionInitialize, and OnGameEnd for
// the quit-to-menu and quit-to-load paths, where no map tick ever comes — #425, #440). While armed, this samples the main
// thread's managed stack at +15s/+30s/+60s and logs the frames. Three samples of a
// deterministic stall name the hot method (a loop shows identical top frames each time).
// Independently disableable via the "Enable Exit Stall Sampler" MCM toggle (the only
// diagnostics component that suspends the main thread).
//
// Thread.Suspend/StackTrace(Thread) are obsolete-but-functional on net472; this is the
// standard in-process diagnostic sampling pattern. Known residual risk: suspending the main
// thread mid-GC and then allocating can deadlock the sampler before Resume — acceptable for
// a dev-machine diagnostic on a 100%-reproducible stall (worst case: kill + retry). The
// whole capture is try/catch'd with Resume in finally, and the sampler only ever runs while
// a mission teardown is actually in progress.
public sealed class ExitStallSampler : IDisposable
{
    private const string Tag = "[ExitStall]";
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    // The known-good tournament exit is ~9.5s (the residual UIExtenderEx-wrapper cost of
    // the engine's template-tree release, measured 2026-07-10 post-fix) — thresholds sit
    // ABOVE it so healthy exits never log a false stall sample, while a regression toward
    // the fixed ~107s class gets three stacks.
    internal static readonly double[] SampleThresholdsSeconds = { 15.0, 30.0, 60.0 };

    private readonly IModLogger _logger;
    private readonly IBattleLoadDiagnosticsSettingsProvider _settings;
    private readonly IBattleLoadDiagnosticsService _service;

    private Timer? _timer;
    private Thread? _mainThread;
    private long _lastWindowTicks;
    private int _samplesTaken;
    private int _pollActive; // reentrancy guard — a blocked capture must not overlap the next Timer tick

    public ExitStallSampler(
        IModLogger logger,
        IBattleLoadDiagnosticsSettingsProvider settings,
        IBattleLoadDiagnosticsService service)
    {
        _logger = logger;
        _settings = settings;
        _service = service;
    }

    /// <summary>Must be called from the game's main thread (SubModule lifecycle methods are).</summary>
    public void SetMainThread(Thread mainThread) => _mainThread = mainThread;

    public void Start()
    {
        if (_timer != null) return;
        _timer = new Timer(_ => Poll(), null, PollInterval, PollInterval);
    }

    // Pure schedule decision: one sample per poll, each waits for its own threshold.
    public static bool ShouldSample(double elapsedSeconds, int samplesTaken)
        => samplesTaken < SampleThresholdsSeconds.Length
           && elapsedSeconds >= SampleThresholdsSeconds[samplesTaken];

    private void Poll()
    {
        // Timer callbacks overlap when a tick outlives the 1s period (a capture can block on
        // logging or, worst case, the documented suspend-mid-GC wedge). Overlapping Polls
        // would race _samplesTaken and interleave Suspend/Resume pairs on the main thread —
        // skip the tick instead (Codex round-2 P2).
        if (Interlocked.Exchange(ref _pollActive, 1) == 1) return;
        try
        {
            if (!_settings.IsEnabled || !_settings.ExitStallSamplerEnabled) return;

            long windowTicks = _service.ExitWindowOpenedUtcTicks;
            if (windowTicks == 0L)
            {
                _samplesTaken = 0;
                _lastWindowTicks = 0L;
                return;
            }

            if (windowTicks != _lastWindowTicks)
            {
                _lastWindowTicks = windowTicks;
                _samplesTaken = 0;
            }

            double elapsed = (DateTime.UtcNow - new DateTime(windowTicks, DateTimeKind.Utc)).TotalSeconds;
            if (!ShouldSample(elapsed, _samplesTaken)) return;

            int sampleIndex = ++_samplesTaken;
            CaptureMainThreadStack(sampleIndex, elapsed);
        }
        catch (Exception ex)
        {
            try { _logger.LogWarning($"{Tag} poll failed: {ex.GetType().Name}: {ex.Message}"); }
            catch { /* never propagate from a timer callback */ }
        }
        finally
        {
            Interlocked.Exchange(ref _pollActive, 0);
        }
    }

    private void CaptureMainThreadStack(int sampleIndex, double elapsedSeconds)
    {
        var thread = _mainThread;
        if (thread == null || !thread.IsAlive)
        {
            _logger.LogWarning($"{Tag} sample#{sampleIndex} skipped — main thread not set/alive");
            return;
        }

        StackTrace? stack = null;
        Exception? captureError = null;
        // Thread.Suspend/Resume + StackTrace(Thread,bool) are obsolete-as-WARNING on net472
        // (present in both the reference assemblies and the runtime — verified empirically,
        // round-2 compat review) — the canonical in-process sampling pattern for a stalled
        // thread, acceptable for this diagnostics-only path. NOTHING between Suspend and
        // Resume may log or otherwise allocate beyond the walk itself — Resume first, then
        // report (Codex round-2 P3: logging inside the suspended window widens the
        // suspend-mid-GC deadlock risk the class header documents).
#pragma warning disable CS0618
        thread.Suspend();
        try
        {
            stack = new StackTrace(thread, needFileInfo: false);
        }
        catch (Exception ex)
        {
            captureError = ex;
        }
        finally
        {
            try { thread.Resume(); }
            catch { /* resume must never throw out */ }
        }
#pragma warning restore CS0618

        if (captureError != null)
            _logger.LogWarning($"{Tag} sample#{sampleIndex} capture failed: {captureError.GetType().Name}: {captureError.Message}");

        if (stack == null) return;

        // Format AFTER resume — keep the suspended window as small as possible.
        var sb = new StringBuilder(2048);
        sb.Append($"{Tag} sample#{sampleIndex} at +{elapsedSeconds:F0}s into exit stall — main thread ({stack.FrameCount} frames):");
        for (int i = 0; i < stack.FrameCount; i++)
        {
            var method = stack.GetFrame(i)?.GetMethod();
            sb.Append("\n    at ");
            sb.Append(method == null ? "<unknown>" : $"{method.DeclaringType?.FullName}.{method.Name}");
        }
        _logger.LogError(sb.ToString());
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
    }
}
