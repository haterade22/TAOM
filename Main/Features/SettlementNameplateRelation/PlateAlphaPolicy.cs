using TAOM.Core.Validation;

namespace TAOM.Features.SettlementNameplateRelation;

/// <summary>
/// The per-frame decision behind <see cref="TaomSettlementPlateWidget"/>'s alpha mirroring, kept
/// pure so the widget carries no logic. Vanilla's <c>SettlementNameplateWidget</c> lerps the item
/// widget's <c>AlphaFactor</c> / <c>ColorFactor</c> every frame; the plate's own sprites live on
/// child widgets that the engine never propagates those values to, so the widget copies them.
/// </summary>
public static class PlateAlphaPolicy
{
    /// <summary>
    /// Vanilla's lowest in-window plate alpha, the neutral target
    /// (<c>SettlementNameplateWidget._normalNeutralAlphaTarget</c>, v1.4.8 dump line 67). Above it the
    /// name text stays fully opaque exactly as vanilla keeps it; below it (only the distance fade
    /// gets there) the text follows the plate down instead of floating over the map alone.
    /// </summary>
    public const float VanillaMinimumPlateAlpha = 0.35f;

    /// <summary>Vanilla's per-frame text lerp lands within a hair of its target; anything closer
    /// than this counts as already applied so a settled plate is not rewritten every frame.</summary>
    public const float BrushAlphaTolerance = 0.001f;

    /// <summary>
    /// True when a destination alpha must be rewritten: it differs from the target beyond the
    /// tolerance, or it is not finite at all. A NaN already sitting in a brush makes every
    /// tolerance compare false, so it has to be its own reason to write (Codex review, #591).
    /// </summary>
    public static bool NeedsWrite(float current, float target)
    {
        if (!FiniteFloatValidator.IsFinite(current))
            return true;

        return System.Math.Abs(current - target) > BrushAlphaTolerance;
    }

    /// <summary>
    /// True when the pair differs from the last applied pair; stores it. Non-finite input is
    /// refused without touching the stored pair, so a poisoned engine frame never reaches a sprite.
    /// A NaN stored pair (the initial state) always reports a change.
    /// </summary>
    public static bool TryTakeChange(float alpha, float colorFactor, ref float lastAlpha, ref float lastColorFactor)
    {
        if (!FiniteFloatValidator.IsFinite(alpha) || !FiniteFloatValidator.IsFinite(colorFactor))
            return false;

        if (alpha == lastAlpha && colorFactor == lastColorFactor)
            return false;

        lastAlpha = alpha;
        lastColorFactor = colorFactor;
        return true;
    }

    /// <summary>Text and banner alpha for a plate alpha against the vanilla anchor: 1 at or above
    /// 0.35, linear to 0 below it, 1 for non-finite input (never hide the name on garbage).</summary>
    public static float TextAlphaFor(float plateAlpha) => TextAlphaFor(plateAlpha, VanillaMinimumPlateAlpha);

    /// <summary>
    /// The same curve anchored on the plate's own resting alpha when that is BELOW vanilla's 0.35
    /// (#596): a player who sets a 10% plate keeps a fully opaque name at close range, and the name
    /// starts following the plate only once the distance fade pulls it under 0.10. A resting value
    /// at or above 0.35, non-finite or non-positive keeps the vanilla anchor, so the default
    /// coloured curves are unchanged and a tracked plate's 0.8 never moves the anchor.
    /// </summary>
    public static float TextAlphaFor(float plateAlpha, float restingAlpha)
    {
        if (!FiniteFloatValidator.IsFinite(plateAlpha))
            return 1f;

        var anchor = VanillaMinimumPlateAlpha;
        if (FiniteFloatValidator.IsFinite(restingAlpha) && restingAlpha > 0f && restingAlpha < VanillaMinimumPlateAlpha)
            anchor = restingAlpha;

        if (plateAlpha >= anchor)
            return 1f;

        if (plateAlpha <= 0f)
            return 0f;

        return plateAlpha / anchor;
    }
}
