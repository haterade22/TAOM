namespace TAOM.Features.SiegePropDiagnostics;

/// <summary>
/// Wraps <see cref="TaomSettings"/> so the service can be unit-tested without MCM loaded.
/// <c>TaomSettings.Instance</c> is null whenever MCM is absent, so both knobs fall back to
/// their compiled defaults — off, because this is a diagnostic and must cost nothing by default.
/// </summary>
public class SiegePropDiagnosticsSettingsProvider : ISiegePropDiagnosticsSettingsProvider
{
    public bool IsEnabled => TaomSettings.Instance?.EnableSiegePropDiagnostics ?? false;

    public bool IsVerbose => TaomSettings.Instance?.SiegePropDiagnosticsVerbose ?? false;
}
