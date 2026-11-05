using TAOM.Core.Validation;

namespace TAOM.Features.SeatOfSeeing.Domain;

/// <summary>What the screen adapter reads for the lift of the view: the order panel is open, the view is first person, the mission is in Battle mode.</summary>
internal readonly record struct SeatRiseRead(bool PanelOpen, bool FirstPerson, bool BattleMode);

/// <summary>The lift of the hero's third-person view while the order panel is open: a raw progress, its smoothstep, and the height (build-spec section 11).</summary>
internal static class SeatPanelLift
{
    internal const float RiseSeconds = 0.5f;
    internal const float SinkSeconds = 0.4f;
    internal const float FieldHeight = 7f;
    internal const float SiegeHeight = 3f;

    internal static float PanelProgress(float progress, bool panelOpen, float dt)
    {
        var current = FiniteFloatValidator.IsFinite(progress) ? Clamp01(progress) : progress > 0f ? 1f : 0f;
        if (!FiniteFloatValidator.IsFinite(dt) || dt <= 0f)
            return current;
        return Clamp01(current + (panelOpen ? dt / RiseSeconds : -dt / SinkSeconds));
    }

    internal static float PanelLiftAmount(float progress)
    {
        if (!FiniteFloatValidator.IsFinite(progress))
            return 0f;
        var p = Clamp01(progress);
        return p * p * (3f - 2f * p);
    }

    /// <summary>The metres the target is raised by, and exactly zero at zero progress because the target lock tests the offset for exact zero.</summary>
    internal static float Lift(float height, float progress) => FiniteFloatValidator.IsFinite(height) ? height * PanelLiftAmount(progress) : 0f;

    /// <summary>A field battle gets the whole height, a siege or a sally out the small one, and anything else none.</summary>
    internal static float HeightFor(bool field, bool siege) => field ? FieldHeight : siege ? SiegeHeight : 0f;

    private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
}
