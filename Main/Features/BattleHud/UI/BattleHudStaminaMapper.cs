using TAOM.Features.Stamina.Domain;

namespace TAOM.Features.BattleHud.UI;

/// <summary>
/// Issue #1497: the player's stamina bar beside the circular HUD. A thin upright bar that empties downward: its
/// height is the share of the pool that is left, and its colour is the band, so the player sees tiring before the
/// lock and sees the pool come back while Exhausted. Pure apart from the band enum, so the rules have unit tests.
/// </summary>
public static class BattleHudStaminaMapper
{
    /// <summary>The fill's height at a full pool, in pixels. The prefab's track is this plus a 2 px margin at each end.</summary>
    public const int TrackHeight = 120;

    public const string FreshColour = "#D8C58AFF";
    public const string WindedColour = "#E0A030FF";
    public const string TiredColour = "#E06020FF";
    public const string ExhaustedColour = "#8A8A8AFF";

    /// <param name="showBar">The MCM switch Show Stamina Bar.</param>
    /// <param name="hasStamina">Stamina runs in this mission and the player's soldier has a pool (StaminaHooks.TryRead).</param>
    public static BattleHudStamina Map(bool showBar, bool hasStamina, float fraction, StaminaBand band)
    {
        if (!showBar || !hasStamina)
            return new BattleHudStamina(false, 0, FreshColour);
        // A share that is not a number draws an empty bar.
        var share = !(fraction > 0f) ? 0f : fraction > 1f ? 1f : fraction;
        return new BattleHudStamina(true, (int)(share * TrackHeight + 0.5f), ColourOf(band));
    }

    public static string ColourOf(StaminaBand band)
    {
        switch (band)
        {
            case StaminaBand.Winded: return WindedColour;
            case StaminaBand.Tired: return TiredColour;
            case StaminaBand.Exhausted: return ExhaustedColour;
            default: return FreshColour;
        }
    }
}

public readonly struct BattleHudStamina
{
    public BattleHudStamina(bool isVisible, int height, string colour)
    {
        IsVisible = isVisible;
        Height = height;
        Colour = colour;
    }

    public bool IsVisible { get; }

    /// <summary>Pixels of the fill, 0 to <see cref="BattleHudStaminaMapper.TrackHeight"/>.</summary>
    public int Height { get; }

    /// <summary>A Gauntlet colour string, #RRGGBBAA.</summary>
    public string Colour { get; }
}
