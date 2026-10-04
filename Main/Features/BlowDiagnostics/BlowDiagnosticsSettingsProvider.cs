namespace TAOM.Features.BlowDiagnostics;

// Reads the MCM page, fail-open to OFF if MCM isn't ready. Unlike the always-on load/save
// diagnostics, this instruments a PER-BLOW hot path, so the default is OFF — a player only
// pays for it while deliberately reproducing a crash. Fail-open to OFF (not ON) for the same
// reason: an MCM hiccup must not silently switch every battle into per-blow durable logging.
public sealed class BlowDiagnosticsSettingsProvider : IBlowDiagnosticsSettingsProvider
{
    // Read per damaging blow even while OFF (Agent_HandleBlowAux_BlowDiag_Patch), so the MCM reference
    // is cached on its first non-null read and read through (BattleBalanceSettingsProvider pattern).
    private BlowDiagnosticsSettings? _settings;
    private BlowDiagnosticsSettings? Settings => _settings ??= BlowDiagnosticsSettings.Instance;

    public BlowDiagnosticsSettingsProvider() { }
    internal BlowDiagnosticsSettingsProvider(BlowDiagnosticsSettings settings) => _settings = settings;

    public bool IsEnabled => Settings?.EnableBlowDiagnostics ?? false;
}
