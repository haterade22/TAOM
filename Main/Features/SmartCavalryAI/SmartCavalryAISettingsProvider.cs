using TAOM.Features;

namespace TAOM.Features.SmartCavalryAI;

public sealed class SmartCavalryAISettingsProvider : ISmartCavalryAISettingsProvider
{
    public bool IsEnabled => TaomSettings.Instance?.EnableSmartCavalryAI ?? false;

    public bool AvoidFriendlies => TaomSettings.Instance?.SmartCavalryAvoidFriendlies ?? true;

    public float ChargeFormationStrictness =>
        SafeClamp(TaomSettings.Instance?.SmartCavalryChargeStrictness, 0.7f, 0.0f, 1.0f);

    public float ReformDistanceAfterCharge =>
        SafeClamp(TaomSettings.Instance?.SmartCavalryReformDistance, 25f, 10f, 80f);

    public float ChargeLineSpacing =>
        SafeClamp(TaomSettings.Instance?.SmartCavalryLineSpacing, 1.2f, 0.8f, 3.0f);

    public bool IsDebugMode => TaomSettings.Instance?.SmartCavalryDebug ?? false;

    /// <summary>
    /// NaN-safe clamp with defaulting. <c>Clamp(NaN, min, max)</c> with the simple-ternary
    /// implementation returns NaN (because both <c>NaN &lt; min</c> and <c>NaN &gt; max</c>
    /// are false) — that NaN propagates into the state machine and never compares true,
    /// freezing the formation in Forming/Reforming for the rest of the battle. NaN/Infinity
    /// inputs (e.g., from a corrupted MCM value or a community config edit) fall back to
    /// the compiled default. Mirrors the Config-Providers-MUST-Validate rule already
    /// applied to RevoltTuning.
    /// </summary>
    private static float SafeClamp(float? value, float defaultValue, float min, float max)
    {
        var v = value ?? defaultValue;
        if (float.IsNaN(v) || float.IsInfinity(v)) return defaultValue;
        return v < min ? min : v > max ? max : v;
    }
}
