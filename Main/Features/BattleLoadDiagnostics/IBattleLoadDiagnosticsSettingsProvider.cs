namespace TAOM.Features.BattleLoadDiagnostics;

// Wraps the MCM static behind an interface so services never read the singleton directly
// (testable; ADR layer rule). All getters fail-open to the "diagnose now" defaults if
// MCM isn't ready yet.
public interface IBattleLoadDiagnosticsSettingsProvider
{
    bool IsEnabled { get; }
    bool StallWatchdogEnabled { get; }
    bool StallWatchdogBundleEnabled { get; }
    double StallWatchdogSeconds { get; }

    /// <summary>Independent gate for the exit-stall stack sampler (#331 round 2) — the only
    /// diagnostics component that suspends the main thread; disableable on its own.</summary>
    bool ExitStallSamplerEnabled { get; }

    /// <summary>Independent gate for ALL session-wide memory telemetry: the periodic
    /// [MemSample] lines and low-headroom WARN (#386) AND the [MemStation] screen-transition
    /// anchors that ride the same switch. Deliberately NOT tied to the master
    /// <see cref="IsEnabled"/> toggle, so turning off battle-load phase logging does not kill
    /// crash-forensics memory sampling. Turning THIS off silences both instruments.</summary>
    bool MemorySamplerEnabled { get; }

    /// <summary>Seconds between [MemSample] emissions (validated 10-120, default 30);
    /// read live per poll so the MCM knob needs no timer rescheduling.</summary>
    double MemorySampleIntervalSeconds { get; }
}
