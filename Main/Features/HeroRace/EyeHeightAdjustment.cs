using TAOM.Core.Validation;

namespace TAOM.Features.HeroRace;

/// <summary>
/// Pure maths for the dwarf eye-height override, split out of <see cref="EyeHeightAdjustmentHook"/>
/// so the arithmetic can be pinned without standing up the hook's collaborators.
///
/// <para>This became worth isolating when the adjustment stopped being a compile-time constant and
/// became an MCM slider. A value the player can drag is untrusted input, and it lands in a camera
/// height the engine consumes natively.</para>
/// </summary>
public static class EyeHeightAdjustment
{
    /// <summary>
    /// Shipped default, in metres relative to the human baseline. Deliberately unchanged from the
    /// compile-time constant this replaced: turning a constant into a slider must not silently
    /// retune every existing install.
    /// </summary>
    public const float DefaultAdjuster = -0.2f;

    /// <summary>Slider bounds. Wider than any sane tuning, narrow enough to stay in the scene.</summary>
    public const float MinAdjuster = -1.5f;

    /// <inheritdoc cref="MinAdjuster"/>
    public const float MaxAdjuster = 1.5f;

    /// <summary>Absolute floor for a resolved eye height, so no combination can put the camera in the floor.</summary>
    public const float MinEyeHeight = 0.3f;

    /// <inheritdoc cref="MinEyeHeight"/>
    public const float MaxEyeHeight = 2.5f;

    /// <summary>
    /// Sanitises a raw settings value. Non-finite falls back to the shipped default rather than
    /// clamping, because a NaN carries no direction to clamp towards.
    /// </summary>
    public static float ClampAdjuster(float raw)
    {
        if (!FiniteFloatValidator.IsFinite(raw))
            return DefaultAdjuster;

        if (raw < MinAdjuster) return MinAdjuster;
        if (raw > MaxAdjuster) return MaxAdjuster;
        return raw;
    }

    /// <summary>
    /// Computes the adjusted eye height for a configured race. Returns false when the inputs cannot
    /// produce a trustworthy result, in which case the caller must leave vanilla eye height alone.
    /// <b>On false, <paramref name="adjusted"/> is always 0 and carries no meaning</b> — an earlier
    /// version left the baseline there on one failure path and 0 on another, which invited a caller
    /// to use the out value without checking the bool.
    /// </summary>
    /// <param name="humanBaseline">The human race eye height this offset is measured against.</param>
    /// <param name="adjuster">Offset in metres. Negative lowers the camera.</param>
    public static bool Resolve(float humanBaseline, float adjuster, out float adjusted)
    {
        adjusted = 0f;

        // Positive requirements throughout, so a NaN fails the gate instead of sliding through an
        // inverted early-exit.
        if (!FiniteFloatValidator.IsFinite(humanBaseline) || !(humanBaseline > 0f))
            return false;

        if (!FiniteFloatValidator.IsFinite(adjuster))
            return false;

        var value = humanBaseline + adjuster;
        if (value < MinEyeHeight) value = MinEyeHeight;
        else if (value > MaxEyeHeight) value = MaxEyeHeight;

        adjusted = value;
        return true;
    }
}
