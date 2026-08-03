namespace TAOM.Features.SiegePropDiagnostics;

public interface ISiegePropDiagnosticsSettingsProvider
{
    /// <summary>Master gate. Off by default — this is a diagnostic, not a gameplay feature.</summary>
    bool IsEnabled { get; }

    /// <summary>Emit one line per prop rather than only the summary and the faults.</summary>
    bool IsVerbose { get; }
}
