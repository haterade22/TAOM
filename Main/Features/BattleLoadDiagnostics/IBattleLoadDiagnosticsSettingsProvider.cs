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

    /// <summary>Independent gate for the battle-freeze stack sampler (#634): photographs a mission
    /// tick or asynchronous agent tick stuck for 10s or more.</summary>
    bool MissionTickStallSamplerEnabled { get; }

    /// <summary>Independent gate for ALL session-wide memory telemetry: the periodic
    /// [MemSample] lines and low-headroom WARN (#386) AND the [MemStation] screen-transition
    /// anchors that ride the same switch. Deliberately NOT tied to the master
    /// <see cref="IsEnabled"/> toggle, so turning off battle-load phase logging does not kill
    /// crash-forensics memory sampling. Turning THIS off silences both instruments.</summary>
    bool MemorySamplerEnabled { get; }

    /// <summary>Seconds between [MemSample] emissions (validated 10-120, default 30);
    /// read live per poll so the MCM knob needs no timer rescheduling.</summary>
    double MemorySampleIntervalSeconds { get; }

    /// <summary>The Patch97 tick profiler's toggle. Unlike its siblings it fails CLOSED to false when
    /// MCM is not ready, because it installs Harmony patches on the two hottest mission methods. Read at
    /// the first game init, where Patch97 installs or is skipped, and again at each mission start: turning
    /// it on needs a restart, turning it off stops behaviour timing from the next mission (the patches stay,
    /// and the hitch probe, when installed and on, still measures).</summary>
    bool TickProfilerEnabled { get; }

    /// <summary>The Patch101 map profiler's toggle. Like the tick profiler's it fails CLOSED to false
    /// when MCM is not ready, because it installs Harmony patches on the campaign map's per-frame methods.
    /// Read at every game init (the first installs or skips Patch101, later ones only report) and at each
    /// campaign session start: turning it on needs a restart, turning it off stops measuring from the next
    /// campaign session (the patches stay).</summary>
    bool MapProfilerEnabled { get; }

    /// <summary>How many behaviours a [TickProfile] or [TickSummary] line lists (validated 1-20,
    /// default 8); read at each mission start. It also sizes the [MapProfile] and [MapProfileSummary]
    /// lists (map tick listeners and TAOM map views), read at each campaign session start.</summary>
    int TickProfilerTopN { get; }

    /// <summary>The frame time at or above which the profiler writes a [Hitch] line (validated
    /// 50-2000 ms, default 250); read at each mission start.</summary>
    double HitchThresholdMs { get; }

    /// <summary>The Patch98 hitch probe's toggle. Fails CLOSED to false when MCM is not ready, because it
    /// installs Harmony patches; the compiled MCM default is true. Read once per process at the first game
    /// init, where Patch98 installs or is skipped, and at each mission start.</summary>
    bool HitchProbeEnabled { get; }

    /// <summary>The detailed load-time stamps (default false); read at game start, game initialization and each campaign dispatch.</summary>
    bool LoadTimeStampsEnabled { get; }
}
