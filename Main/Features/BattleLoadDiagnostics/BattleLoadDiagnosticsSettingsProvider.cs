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
    private const int DefaultTickProfilerTopN = 8;
    private const int MinTickProfilerTopN = 1;
    private const int MaxTickProfilerTopN = 20;
    private const double DefaultHitchThresholdMs = 250d;
    private const double MinHitchThresholdMs = 50d;
    private const double MaxHitchThresholdMs = 2000d;

    public bool IsEnabled =>
        BattleLoadDiagnosticsSettings.Instance?.EnableBattleLoadDiagnostics ?? true;

    public bool StallWatchdogEnabled =>
        BattleLoadDiagnosticsSettings.Instance?.EnableStallWatchdog ?? true;

    public bool StallWatchdogBundleEnabled =>
        BattleLoadDiagnosticsSettings.Instance?.EnableStallWatchdogBundle ?? true;

    public bool ExitStallSamplerEnabled =>
        BattleLoadDiagnosticsSettings.Instance?.EnableExitStallSampler ?? true;

    public bool MissionTickStallSamplerEnabled =>
        BattleLoadDiagnosticsSettings.Instance?.EnableMissionTickStallSampler ?? true;

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

    public bool MemorySamplerEnabled =>
        BattleLoadDiagnosticsSettings.Instance?.EnableMemorySampler ?? true;

    public double MemorySampleIntervalSeconds
    {
        get
        {
            double raw = BattleLoadDiagnosticsSettings.Instance?.MemorySampleIntervalSeconds
                ?? (int)MemoryPressureSampler.DefaultSampleIntervalSeconds;
            return ValidateSampleIntervalSeconds(raw);
        }
    }

    // Pure seam (StallWatchdogSeconds pattern, testable without the MCM static). Bounds and
    // default live on MemoryPressureSampler — the single source of truth for the contract.
    internal static double ValidateSampleIntervalSeconds(double raw) =>
        FiniteFloatValidator.IsFiniteInRange(
            raw, MemoryPressureSampler.MinSampleIntervalSeconds, MemoryPressureSampler.MaxSampleIntervalSeconds)
            ? raw
            : MemoryPressureSampler.DefaultSampleIntervalSeconds;

    // Fail-CLOSED, unlike the getters above: this toggle installs Patch97 on Mission.OnTick and
    // Mission.OnPreTick, so "MCM not ready" must never install it.
    public bool TickProfilerEnabled =>
        BattleLoadDiagnosticsSettings.Instance?.EnableTickProfiler ?? false;

    public int TickProfilerTopN =>
        ValidateTickProfilerTopN(BattleLoadDiagnosticsSettings.Instance?.TickProfilerTopN ?? DefaultTickProfilerTopN);

    public double HitchThresholdMs =>
        ValidateHitchThresholdMs(BattleLoadDiagnosticsSettings.Instance?.HitchThresholdMs ?? (int)DefaultHitchThresholdMs);

    internal static int ValidateTickProfilerTopN(int raw) =>
        raw >= MinTickProfilerTopN && raw <= MaxTickProfilerTopN ? raw : DefaultTickProfilerTopN;

    internal static double ValidateHitchThresholdMs(double raw) =>
        FiniteFloatValidator.IsFiniteInRange(raw, MinHitchThresholdMs, MaxHitchThresholdMs)
            ? raw
            : DefaultHitchThresholdMs;
}
