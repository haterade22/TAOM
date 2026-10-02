using TAOM.Core.Validation;

namespace TAOM.Features.FactionUI.UI;

/// <summary>The frame time the themed screens' animations advance by (#704).</summary>
public static class FrameTime
{
    /// <summary>The engine's frame time when it is a finite, non-negative number; otherwise 0, so the
    /// animations hold for that frame. A NaN would otherwise freeze every animation mid-way for good
    /// (its "done" test never passes) and write NaN into the widgets' alpha and offsets every frame.</summary>
    public static float Sanitize(float dt) => FiniteFloatValidator.IsFiniteAtLeast(dt, 0f) ? dt : 0f;
}
