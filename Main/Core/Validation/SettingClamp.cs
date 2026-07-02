namespace TAOM.Core.Validation;

/// <summary>
/// Shared null-safe, NaN-safe clamp for MCM setting reads — the standard settings-provider recipe
/// (`TaomSettings.Instance?.Knob` → default when MCM isn't loaded, reject non-finite, clamp to the
/// knob's engine-valid range). Consolidates the byte-identical private SafeClamp/SafeClampInt helpers
/// previously copy-pasted across SmartCavalryAI / BanditManagement / CultureConversion /
/// CastleRecruitment providers (R2, 2026-07-01).
///
/// Why the NaN guard matters (float overload): <c>Clamp(NaN, min, max)</c> with the simple-ternary
/// implementation returns NaN (both <c>NaN &lt; min</c> and <c>NaN &gt; max</c> are false) — the NaN
/// then propagates into consumers and never compares true (the SmartCavalryAI frozen-formation bug
/// class). Non-finite input falls back to the compiled default. Only the VALUE is validated; the
/// compiled default is trusted verbatim. Sibling predicates: <see cref="FiniteFloatValidator"/>.
/// </summary>
public static class SettingClamp
{
    public static float Clamp(float? value, float defaultValue, float min, float max)
    {
        var v = value ?? defaultValue;
        if (float.IsNaN(v) || float.IsInfinity(v)) return defaultValue;
        return v < min ? min : v > max ? max : v;
    }

    public static int Clamp(int? value, int defaultValue, int min, int max)
    {
        var v = value ?? defaultValue;
        return v < min ? min : v > max ? max : v;
    }
}
