using System;
using System.Threading;
using TAOM.Core.Logging;
using TAOM.Features.BattleLoadDiagnostics.Domain;

namespace TAOM.Features.BattleLoadDiagnostics;

// Session-wide memory-pressure telemetry (#386). Bannerlord CTDs with no managed culprit are
// frequently commit-exhaustion deaths (native allocator fails, the AV surfaces far from the
// cause) — this stamps a periodic [MemSample] line so the tail of any crash log shows the
// memory trajectory, and a one-shot WARN when commit headroom runs low. Same shape as
// BattleLoadStallWatchdog: thread-pool Timer, swallow-and-warn callback, pure static decision
// seams unit-tested per ADR-008.
//
// The line literals + thresholds are a cross-lane contract: tools/triage_battle_load.py
// (tests: tools/tests/test_triage_battle_load.py) parses them, and its threshold mirror names
// this class. Change them here first, then the Python twin.
public sealed class MemoryPressureSampler : IDisposable
{
    private const string Tag = "[MemSample]";
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    // Single source of truth for the low-headroom contract (the Python triage mirror cites
    // this class). Headroom = sysCommitLimitMB - sysCommitUsedMB; low when it is under the
    // floor OR under WarnHeadroomPercent of the limit.
    internal const long WarnHeadroomFloorMb = 2048;
    internal const int WarnHeadroomPercent = 10;
    internal const long RearmHysteresisMb = 512;

    // Emit cadence (the 5s poll only decides; these govern how often a line is written).
    internal const double DefaultSampleIntervalSeconds = 30d;
    internal const double MinSampleIntervalSeconds = 10d;
    internal const double MaxSampleIntervalSeconds = 120d;

    private readonly IModLogger _logger;
    private readonly IBattleLoadDiagnosticsSettingsProvider _settings;

    private Timer? _timer;
    private int _pollActive; // reentrancy guard (ExitStallSampler precedent) — a blocked tick must not overlap the next
    private long _lastEmitUtcTicks; // 0 = never emitted — first enabled poll samples immediately
    private bool _sessionLineEmitted;
    private bool _warnLatched;
    private bool _readFailureWarned;

    public MemoryPressureSampler(IModLogger logger, IBattleLoadDiagnosticsSettingsProvider settings)
    {
        _logger = logger;
        _settings = settings;
    }

    public void Start()
    {
        if (_timer != null) return;
        _timer = new Timer(_ => Poll(), null, PollInterval, PollInterval);
    }

    private void Poll()
    {
        if (Interlocked.Exchange(ref _pollActive, 1) == 1) return;
        try { PollOnce(DateTime.UtcNow); }
        finally { Interlocked.Exchange(ref _pollActive, 0); }
    }

    // Internal seam so tests drive the cadence deterministically; swallow-and-warn lives HERE
    // (not in Poll) so the tested path proves a throwing logger cannot propagate.
    internal void PollOnce(DateTime utcNow)
    {
        try { PollCore(utcNow); }
        catch (Exception ex)
        {
            try { _logger.LogWarning($"{Tag} poll failed: {ex.GetType().Name}: {ex.Message}"); }
            catch { /* never propagate from a timer callback */ }
        }
    }

    private void PollCore(DateTime utcNow)
    {
        // Gate ONLY on the sampler's own toggle — deliberately NOT the master IsEnabled.
        // The master toggle governs battle-load PHASE logging; this sampler is session-wide
        // crash forensics (the memory trajectory before a native OOM CTD), and turning off
        // phase logging must not silently kill it. Its own MCM toggle is the kill switch.
        if (!_settings.MemorySamplerEnabled) return;

        long last = _lastEmitUtcTicks;
        double secondsSinceLastEmit = last == 0L
            ? double.MaxValue // never emitted — sample on the first enabled poll
            : (utcNow - new DateTime(last, DateTimeKind.Utc)).TotalSeconds;

        // Read the interval LIVE each poll — the MCM knob takes effect without timer rescheduling.
        if (!ShouldSample(secondsSinceLastEmit, _settings.MemorySampleIntervalSeconds)) return;

        if (!MemorySampleReader.TryRead(out var sample))
        {
            if (!_readFailureWarned)
            {
                _readFailureWarned = true;
                _logger.LogWarning($"{Tag} reader failed — memory telemetry unavailable (will keep polling)");
            }
            return;
        }
        _readFailureWarned = false;

        if (!_sessionLineEmitted)
        {
            _sessionLineEmitted = true;
            _logger.LogInfo(FormatSession(in sample));
        }

        if (ShouldWarn(sample.SysCommitUsedMb, sample.SysCommitLimitMb, _warnLatched))
        {
            _warnLatched = true;
            _logger.LogWarning(FormatWarn(in sample));
        }
        else
        {
            if (ShouldRearm(sample.SysCommitUsedMb, sample.SysCommitLimitMb, _warnLatched))
                _warnLatched = false;
            _logger.LogInfo(FormatSample(in sample));
        }

        _lastEmitUtcTicks = utcNow.Ticks;
    }

    // ---- Pure decision seams -------------------------------------------------------------

    internal static bool ShouldSample(double secondsSinceLastEmit, double intervalSeconds)
        => secondsSinceLastEmit >= intervalSeconds;

    // Low-headroom polarity: garbage inputs (non-positive limit, negative used) must NOT
    // report low — no WARN may ever be computed from garbage. used > limit is not garbage
    // (an over-committed reading); its headroom clamps to 0, which IS low.
    internal static bool IsLowHeadroom(long usedMb, long limitMb)
    {
        if (limitMb <= 0 || usedMb < 0) return false;
        return HeadroomMb(usedMb, limitMb) < WarnThresholdMb(limitMb);
    }

    internal static bool ShouldWarn(long usedMb, long limitMb, bool latched)
        => !latched && IsLowHeadroom(usedMb, limitMb);

    // Rearm only once headroom clears the warn threshold by the hysteresis margin, so a
    // reading oscillating around the threshold cannot spam one WARN per interval.
    internal static bool ShouldRearm(long usedMb, long limitMb, bool latched)
    {
        if (!latched) return false;
        if (limitMb <= 0 || usedMb < 0) return false; // garbage never flips the latch
        return HeadroomMb(usedMb, limitMb) >= WarnThresholdMb(limitMb) + RearmHysteresisMb;
    }

    private static long HeadroomMb(long usedMb, long limitMb)
        => Math.Max(0L, limitMb - usedMb);

    private static long WarnThresholdMb(long limitMb)
        => Math.Max(WarnHeadroomFloorMb, limitMb * WarnHeadroomPercent / 100);

    // ---- Pure format seams (the pinned cross-lane literals) --------------------------------

    internal static string FormatSession(in MemorySample s)
        => $"{Tag} session totalPhysMB={s.TotalPhysMb} sysCommitLimitMB={s.SysCommitLimitMb}";

    internal static string FormatSample(in MemorySample s)
        => $"{Tag} privMB={s.PrivMb} wsMB={s.WsMb} heapMB={s.HeapMb}" +
           $" sysCommitUsedMB={s.SysCommitUsedMb} sysCommitLimitMB={s.SysCommitLimitMb}" +
           $" availPhysMB={s.AvailPhysMb} memLoad={s.MemLoadPercent}%";

    internal static string FormatWarn(in MemorySample s)
        => $"{Tag} WARN LOW COMMIT HEADROOM headroomMB={HeadroomMb(s.SysCommitUsedMb, s.SysCommitLimitMb)}" +
           $" privMB={s.PrivMb} wsMB={s.WsMb} heapMB={s.HeapMb}" +
           $" sysCommitUsedMB={s.SysCommitUsedMb} sysCommitLimitMB={s.SysCommitLimitMb}" +
           $" availPhysMB={s.AvailPhysMb} memLoad={s.MemLoadPercent}%";

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
    }
}
