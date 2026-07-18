namespace TAOM.Features.BlowDiagnostics;

// Isolates the MCM read so the service is unit-testable without a live MCM page.
public interface IBlowDiagnosticsSettingsProvider
{
    bool IsEnabled { get; }
}
