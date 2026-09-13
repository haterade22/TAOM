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

    /// <summary>Text and banner alpha for a plate alpha: 1 at or above the vanilla minimum,
    /// linear to 0 below it, 1 for non-finite input (never hide the name on garbage).</summary>
    public static float TextAlphaFor(float plateAlpha)
    {
        if (!FiniteFloatValidator.IsFinite(plateAlpha))
            return 1f;

        if (plateAlpha >= VanillaMinimumPlateAlpha)
            return 1f;

        if (plateAlpha <= 0f)
            return 0f;

        return plateAlpha / VanillaMinimumPlateAlpha;
    }
}
