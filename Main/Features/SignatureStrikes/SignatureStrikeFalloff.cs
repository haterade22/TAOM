using TAOM.Core.Validation;

namespace TAOM.Features.SignatureStrikes;

/// <summary>
/// The engine's own area-damage curve, lifted from <c>Mission.MissileAreaDamageCallback</c>
/// (v1.5.3 Mission.cs:5739-5744): full damage inside the inner radius, then
/// <c>1 / lerp(1, 3, t)^2</c> across the band, so the outer edge takes one ninth. A boulder uses
/// 1.0 m / 1.2 m; the config chooses the radii here.
/// </summary>
public static class SignatureStrikeFalloff
{
    /// <summary>
    /// Multiplier in [0, 1] for a victim <paramref name="distance"/> metres from the impact. Zero
    /// beyond <paramref name="outerRadius"/>, zero for any non-finite or negative input (every
    /// argument is an engine or config float; NaN must fail closed, never propagate).
    /// </summary>
    public static float Compute(float distance, float innerRadius, float outerRadius)
    {
        if (!FiniteFloatValidator.IsFinite(distance)
            || !FiniteFloatValidator.IsFinite(innerRadius)
            || !FiniteFloatValidator.IsFinite(outerRadius))
            return 0f;

        // Positive requirements: a NaN would have passed an inverted `< 0` early-exit.
        if (!(distance >= 0f) || !(outerRadius > 0f) || !(distance <= outerRadius))
            return 0f;

        // Degenerate band (the provider rejects inner > outer; degrade to flat if seen anyway).
        if (!(innerRadius < outerRadius) || distance <= innerRadius)
            return 1f;

        var t = (distance - innerRadius) / (outerRadius - innerRadius);
        var k = 1f + 2f * t;
        return 1f / (k * k);
    }
}
