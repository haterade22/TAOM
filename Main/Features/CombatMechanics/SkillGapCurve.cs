using System;

namespace TAOM.Features.CombatMechanics;

// The weapon-skill gap curve shared by crush-through and Cleaving Blows: 0 inside the dead zone, rising along
// 1 - e^(-(gap - deadZone) x exponent) to exactly 1 at the target delta. Pure math, no allocation.
internal static class SkillGapCurve
{
    // The float shape of the numerator, so a gap equal to the target delta gives a ratio of exactly 1.0. Non-positive means a
    // misconfigured curve; Normalized then answers 0.
    public static double Denominator(int deadZone, int targetDelta, float exponent)
    {
        float curveSpan = targetDelta - deadZone;
        float denominatorArg = curveSpan * exponent;
        return 1.0 - Math.Exp(-denominatorArg);
    }

    public static float Normalized(float delta, int deadZone, float exponent, double denominator)
    {
        if (!(delta > deadZone) || denominator <= 0.0)
            return 0f;

        float numeratorArg = (delta - deadZone) * exponent;
        double normalized = (1.0 - Math.Exp(-numeratorArg)) / denominator;
        return Clamp01((float)normalized);
    }

    private static float Clamp01(float value)
    {
        if (value < 0f)
            return 0f;
        return value > 1f ? 1f : value;
    }
}
