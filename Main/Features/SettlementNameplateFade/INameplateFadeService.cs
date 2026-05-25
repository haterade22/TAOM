namespace TAOM.Features.SettlementNameplateFade;

/// <summary>
/// Computes the alpha multiplier to apply to a settlement nameplate's target alpha based on
/// the camera distance to the settlement. Pure function — no state, fully unit-testable.
///
/// Returned multiplier is in [0, 1]. The Harmony patch multiplies the vanilla
/// <c>DetermineTargetAlphaValue()</c> result by this number, so the per-relation/per-tracked
/// alpha tiers are preserved and only get scaled down with distance.
/// </summary>
public interface INameplateFadeService
{
    /// <summary>
    /// Returns the fade multiplier for the given camera distance.
    /// <list type="bullet">
    /// <item><c>distance &lt;= NearDistance</c> → returns 1.0 (no fade).</item>
    /// <item><c>distance &gt;= FarDistance</c> → returns 0.0 (fully hidden).</item>
    /// <item>between → linear fade from 1.0 down to 0.0.</item>
    /// <item>If feature disabled, NaN/Infinity input, or negative distance — returns 1.0 (safe fallback).</item>
    /// </list>
    /// </summary>
    float ComputeAlphaMultiplier(float distanceToCamera);
}
