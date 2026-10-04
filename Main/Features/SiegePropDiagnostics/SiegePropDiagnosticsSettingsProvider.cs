namespace TAOM.Features.SiegePropDiagnostics;

/// <summary>
/// Wraps <see cref="TaomSettings"/> so the service can be unit-tested without MCM loaded.
/// <c>TaomSettings.Instance</c> is null whenever MCM is absent, so both knobs fall back to
/// their compiled defaults — off, because this is a diagnostic and must cost nothing by default.
/// </summary>
public class SiegePropDiagnosticsSettingsProvider : ISiegePropDiagnosticsSettingsProvider
{
    // Read every frame by SiegePropDiagnosticsMissionBehavior: cached on the first non-null read, read
    // through (BattleBalanceSettingsProvider pattern).
    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public SiegePropDiagnosticsSettingsProvider() { }
    internal SiegePropDiagnosticsSettingsProvider(TaomSettings settings) => _settings = settings;

    public bool IsEnabled => Settings?.EnableSiegePropDiagnostics ?? false;

    public bool IsVerbose => Settings?.SiegePropDiagnosticsVerbose ?? false;
}
