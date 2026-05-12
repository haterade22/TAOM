namespace TAOM.Core.Validation;

/// <summary>
/// Centralized float-validation helpers for config providers.
///
/// Why this exists: range checks like `value &lt; min || value &gt; max` evaluate false for `NaN`
/// (all NaN comparisons return false per IEEE-754), so a `NaN` config value sneaks past validation
/// and then breaks downstream comparisons in unpredictable ways. This has shipped twice:
///
/// * Career cooldown review #31 (2026-05-04) — NaN cooldown made `IsOnCooldown =&gt; CooldownRemaining &gt; 0f`
///   evaluate false → ability "always ready" → V re-activates indefinitely.
/// * EditorCacheRebuild Codex review #38 (2026-05-12) — NaN `SmokeTestDistanceTolerance` made the gate's
///   `maxDelta &gt; tolerance` evaluate false → smoke test silently disabled → potential threading
///   issues never caught.
///
/// Use these helpers BEFORE every range check on a `float`/`double` config field. Bool/int fields
/// don't need this — only IEEE-754 types are affected.
/// </summary>
public static class FiniteFloatValidator
{
    /// <summary>Returns true if <paramref name="value"/> is a real, finite number (not NaN, not ±Infinity).</summary>
    public static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);

    /// <summary>Returns true if <paramref name="value"/> is a real, finite number (not NaN, not ±Infinity).</summary>
    public static bool IsFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value);

    /// <summary>
    /// Returns true if <paramref name="value"/> is finite AND within [min, max] (inclusive).
    /// NaN/Infinity always return false. Standard pattern for config range validation.
    /// </summary>
    public static bool IsFiniteInRange(float value, float min, float max) =>
        IsFinite(value) && value >= min && value <= max;

    /// <summary>
    /// Returns true if <paramref name="value"/> is finite AND less than or equal to <paramref name="max"/>.
    /// Use for penalty fields constrained to be non-positive (e.g., loyalty penalties must be ≤ 0).
    /// NaN/Infinity always return false.
    /// </summary>
    public static bool IsFiniteAtMost(float value, float max) =>
        IsFinite(value) && value <= max;

    /// <summary>
    /// Returns true if <paramref name="value"/> is finite AND greater than or equal to <paramref name="min"/>.
    /// Use for bonus fields constrained to be non-negative.
    /// NaN/Infinity always return false.
    /// </summary>
    public static bool IsFiniteAtLeast(float value, float min) =>
        IsFinite(value) && value >= min;
}
