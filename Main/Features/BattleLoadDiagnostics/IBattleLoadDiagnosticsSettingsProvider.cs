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
}
