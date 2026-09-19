using System;
using System.Globalization;
using TAOM.Core.Validation;

namespace TAOM.Features.Elephant;

/// <summary>
/// Arithmetic behind the howdah diagnostics log (#627). Pure: the machine passes plain floats read from the engine, so
/// this stays unit-tested and free of TaleWorlds types. Every result that can see a non-finite input comes back NaN
/// instead of a number computed from garbage, and the log prints it as "n/a".
/// </summary>
public static class HowdahDiagnostics
{
    /// <summary>World z of the top of an agent's body capsule: feet z, plus the higher of the two capsule points
    /// (Monster.BodyCapsulePoint1/2 are in the agent's upright frame), plus the radius. NaN if any input is not finite.</summary>
    public static float CapsuleTopZ(float feetZ, float point1Z, float point2Z, float radius)
    {
        if (!FiniteFloatValidator.IsFinite(feetZ) || !FiniteFloatValidator.IsFinite(point1Z)
            || !FiniteFloatValidator.IsFinite(point2Z) || !FiniteFloatValidator.IsFinite(radius))
            return float.NaN;
        return feetZ + Math.Max(point1Z, point2Z) + radius;
    }

    /// <summary>How far a body's z sits above a capsule top: negative means inside the capsule. NaN if not computable.</summary>
    public static float Clearance(float bodyZ, float capsuleTopZ)
    {
        if (!FiniteFloatValidator.IsFinite(bodyZ) || !FiniteFloatValidator.IsFinite(capsuleTopZ)) return float.NaN;
        return bodyZ - capsuleTopZ;
    }

    /// <summary>A positive requirement: only a proven clearance of zero or more stays quiet, so NaN warns too.</summary>
    public static bool ClearanceWarrantsWarning(float clearance) => !(clearance >= 0f);

    /// <summary>Straight-line distance between two points. NaN if any coordinate is not finite.</summary>
    public static float Distance(float ax, float ay, float az, float bx, float by, float bz)
    {
        float dx = bx - ax, dy = by - ay, dz = bz - az;
        float d = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        return FiniteFloatValidator.IsFinite(d) ? d : float.NaN;
    }

    /// <summary>A log-safe number: invariant culture (a German client would print commas), "n/a" when not finite.</summary>
    public static string Format(float value, int decimals) =>
        FiniteFloatValidator.IsFinite(value)
            ? value.ToString("F" + decimals, CultureInfo.InvariantCulture)
            : "n/a";
}
