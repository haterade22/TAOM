using TAOM.Core.Validation;

namespace TAOM.Features.WarChronicle.Effects;

/// <summary>The clamps and the strength rule, in one place so the MCM provider and the registry cannot drift.</summary>
internal static class WarEffectMath
{
    internal const float StrengthMin = 0f;
    internal const float StrengthMax = 2f;
    internal const float StrengthDefault = 1f;

    /// <summary>Non-finite becomes the default (1), anything else is clamped into 0..2.</summary>
    internal static float SanitizeStrength(float strength)
    {
        if (!FiniteFloatValidator.IsFinite(strength))
            return StrengthDefault;
        if (strength < StrengthMin)
            return StrengthMin;
        return strength > StrengthMax ? StrengthMax : strength;
    }

    /// <summary>
    /// The volunteer rate stays within half to one and a half, the escape roll within one to three times
    /// (both against TAOM's own base, not a perked one). A kind without a range of its own here is 1, no
    /// effect: a new kind must name its clamp rather than inherit another kind's.
    /// </summary>
    internal static float Clamp(WarEffectKind kind, float multiplier)
    {
        float min, max;
        switch (kind)
        {
            case WarEffectKind.VolunteerRate:
                min = 0.5f;
                max = 1.5f;
                break;
            case WarEffectKind.PrisonerEscape:
                min = 1f;
                max = 3f;
                break;
            default:
                return 1f;
        }

        if (multiplier < min) return min;
        return multiplier > max ? max : multiplier;
    }
}
