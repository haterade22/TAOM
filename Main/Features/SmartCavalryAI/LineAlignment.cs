using System.Collections.Generic;

namespace TAOM.Features.SmartCavalryAI;

/// <summary>
/// The alignment decision the cavalry state machine gates Forming and Reforming on, kept pure
/// so it is tested against numbers. A formation counts as aligned when the MEAN distance from
/// each rider to its own arrangement slot is under a tolerance derived from the MCM strictness.
///
/// <para>Why mean distance to slot, and not spread across the line: the port's original metric
/// projected riders onto the line's own axis and asked for 1.5 m of spread, which a line of more
/// than two riders can never satisfy, so the machine froze every formation on its first F3.
/// Distance to slot is arrangement-agnostic (Line, Skein, Wedge) and is exactly what the engine
/// drives each rider toward under a Move order. Mean rather than max so one straggler cannot hold
/// the whole charge; the timeout in the state machine is the floor for everything else.</para>
/// </summary>
public static class LineAlignment
{
    public const float MinToleranceMeters = 2f;
    public const float ToleranceRangeMeters = 8f;

    /// <summary>Metres of mean slot distance allowed at the given strictness: 10 m at 0,
    /// 4.4 m at the 0.7 default, 2 m at 1. Strictness is clamped to [0, 1].</summary>
    public static float Tolerance(float strictness)
    {
        var s = strictness < 0f ? 0f : strictness > 1f ? 1f : strictness;
        return MinToleranceMeters + ToleranceRangeMeters * (1f - s);
    }

    /// <summary>True when the mean of <paramref name="slotDistances"/> is under
    /// <see cref="Tolerance"/>. An empty list is aligned (nothing to line up). Written as a
    /// positive requirement so a NaN or infinite distance fails the gate instead of passing it.</summary>
    public static bool IsAligned(IReadOnlyList<float> slotDistances, float strictness)
    {
        if (slotDistances == null || slotDistances.Count == 0) return true;
        var sum = 0f;
        for (var i = 0; i < slotDistances.Count; i++) sum += slotDistances[i];
        var mean = sum / slotDistances.Count;
        return mean < Tolerance(strictness);
    }
}
