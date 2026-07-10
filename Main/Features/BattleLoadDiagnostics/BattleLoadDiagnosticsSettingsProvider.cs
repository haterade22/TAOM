using TAOM.Core.Validation;

namespace TAOM.Features.BattleLoadDiagnostics;

// Reads the MCM page, fail-open to defaults if MCM isn't ready. The watchdog threshold
// is range/NaN-guarded per the config-validation rule (belt-and-braces — the MCM integer
// attribute already clamps 10..300, but a provider must never pass an unvalidated value
// into a comparison).
public sealed class BattleLoadDiagnosticsSettingsProvider : IBattleLoadDiagnosticsSettingsProvider
{
    private const double DefaultWatchdogSeconds = 300d;
    private const double MinWatchdogSeconds = 10d;
    private const double MaxWatchdogSeconds = 600d;

    public bool IsEnabled =>
        BattleLoadDiagnosticsSettings.Instance?.EnableBattleLoadDiagnostics ?? true;

    public bool StallWatchdogEnabled =>
        BattleLoadDiagnosticsSettings.Instance?.EnableStallWatchdog ?? true;

    public bool StallWatchdogBundleEnabled =>
        BattleLoadDiagnosticsSettings.Instance?.EnableStallWatchdogBundle ?? true;

    public bool ExitStallSamplerEnabled =>
        BattleLoadDiagnosticsSettings.Instance?.EnableExitStallSampler ?? true;

    public double StallWatchdogSeconds
    {
        get
        {
            double raw = BattleLoadDiagnosticsSettings.Instance?.StallWatchdogSeconds ?? (int)DefaultWatchdogSeconds;
            return FiniteFloatValidator.IsFiniteInRange(raw, MinWatchdogSeconds, MaxWatchdogSeconds)
                ? raw
                : DefaultWatchdogSeconds;
        }
    }
}
