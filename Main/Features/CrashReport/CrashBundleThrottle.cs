using System;
using System.Collections.Generic;

namespace TAOM.Features.CrashReport;

// Decision for a single crash capture: should it produce a bundle ZIP?
public enum CrashBundleDecision
{
    WriteBundle,       // first time this signature is captured (and within cap + cooldown)
    SuppressDuplicate, // this signature already got a bundle this session
    SuppressCap,       // the session bundle cap is reached
    SuppressCooldown,  // a NEW signature arrived too soon after the last bundle write
}

// Result of CrashBundleThrottle.Admit — the decision plus the running occurrence
// count for this signature (used only for the suppression log line).
public readonly struct CrashBundleAdmission
{
    public CrashBundleDecision Decision { get; }
    public int Occurrence { get; }

    public CrashBundleAdmission(CrashBundleDecision decision, int occurrence)
    {
        Decision = decision;
        Occurrence = occurrence;
    }
}

// Bounds how many crash bundle ZIPs the session writes. Without this, a crash that
// recurs every tick (the normal failure mode for a broken mission/campaign) writes a
// fresh bundle on every frame — the "hundreds of taom_crash_*.zip" bug.
//
// Three layers, all session-scoped (reset only on a fresh game launch):
//   1. Dedup    — a signature that already got a bundle is never re-bundled.
//   2. Cap      — at most maxBundlesPerSession distinct bundles, full stop.
//   3. Cooldown — a NEW signature within minInterval of the last write is held off
//                 (but NOT marked bundled, so it can still get its one bundle later).
// The very first bundle is never cooldown-gated, so the real crash is always captured.
//
// Pure + clock-injected for deterministic tests. Thread-safe: the same singleton is
// reachable from off-main-thread AppDomain captures, so all state is lock-guarded.
public sealed class CrashBundleThrottle
{
    private readonly int _maxBundlesPerSession;
    private readonly TimeSpan _minInterval;
    private readonly Func<DateTime> _clock;
    private readonly object _gate = new object();
    private readonly Dictionary<string, int> _counts = new Dictionary<string, int>();
    private readonly HashSet<string> _bundled = new HashSet<string>();
    private int _bundlesWritten;
    private DateTime _lastBundleUtc = DateTime.MinValue;

    public CrashBundleThrottle(int maxBundlesPerSession, TimeSpan minInterval, Func<DateTime> clock)
    {
        _maxBundlesPerSession = maxBundlesPerSession;
        _minInterval = minInterval;
        _clock = clock ?? (() => DateTime.UtcNow);
    }

    public CrashBundleAdmission Admit(string signature)
    {
        signature ??= "(unknown)";
        lock (_gate)
        {
            int count = (_counts.TryGetValue(signature, out var c) ? c : 0) + 1;
            _counts[signature] = count;

            if (_bundled.Contains(signature))
                return new CrashBundleAdmission(CrashBundleDecision.SuppressDuplicate, count);

            if (_bundlesWritten >= _maxBundlesPerSession)
                return new CrashBundleAdmission(CrashBundleDecision.SuppressCap, count);

            var now = _clock();
            if (_bundlesWritten > 0 && now - _lastBundleUtc < _minInterval)
                return new CrashBundleAdmission(CrashBundleDecision.SuppressCooldown, count);

            _bundled.Add(signature);
            _bundlesWritten++;
            _lastBundleUtc = now;
            return new CrashBundleAdmission(CrashBundleDecision.WriteBundle, count);
        }
    }
}
