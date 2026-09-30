namespace TAOM.Features.RealmBorders.Domain;

/// <summary>How a border line is drawn. Chosen per line by <see cref="BorderStyleSelector"/>.</summary>
public enum LineStyle
{
    /// <summary>Watercolour outline colouring on each side and a dip-pen dash-dot on the line: the default.</summary>
    Atlas = 0,

    /// <summary>Two solid realm bands with a gap on the line, as the Kingdom Borders mod drew them.</summary>
    Heraldic = 1,

    /// <summary>An ember glow across a front where the player's side meets an enemy (war map mode).</summary>
    WarFront = 2,
}

/// <summary>What one line gets: its style, the colour on each side, and the two overlays.</summary>
public readonly record struct LinePaint(LineStyle Style, uint LeftColour, uint RightColour, bool Gilded, bool Emphasised);

/// <summary>
/// Widths (world units) and colours (ARGB) of every look, the values to tune in the in-game look
/// session; <see cref="WidthScale"/> is the player's setting. The Atlas values come from the approved
/// mock-up. The Heraldic values are derived from them, so switching looks keeps a border's footprint:
/// the gap is the Atlas ink line's width and each band is half the Atlas wash.
/// </summary>
public sealed class BorderLook
{
    public float WidthScale { get; init; } = 1f;

    public float WashWidth { get; init; } = 2.6f;

    public float WashAlpha { get; init; } = 0.8f;

    public float InkHalfWidth { get; init; } = 0.28f;

    public float DashLength { get; init; } = 1.6f;

    public float DotLength { get; init; } = 0.35f;

    public float PatternGap { get; init; } = 0.55f;

    /// <summary>The Atlas ink line's width, twice <see cref="InkHalfWidth"/>.</summary>
    public float BandGap { get; init; } = 0.56f;

    /// <summary>Half the Atlas <see cref="WashWidth"/>.</summary>
    public float BandWidth { get; init; } = 1.3f;

    public float BandAlpha { get; init; } = 0.9f;

    public float KeylineWidth { get; init; } = 0.1f;

    public float GoldHalfWidth { get; init; } = 0.4f;

    public float EmberWidth { get; init; } = 2.6f;

    public float EmberCoreHalfWidth { get; init; } = 0.2f;

    public uint InkColour { get; init; } = 0xFF2E1C10;

    public uint KeylineColour { get; init; } = 0xCC1E1812;

    public uint GoldColour { get; init; } = 0xFFE3B64A;

    public uint GoldEdgeColour { get; init; } = 0xFF8A6420;

    public uint EmberColour { get; init; } = 0xFFFF7A2A;

    public uint EmberCoreColour { get; init; } = 0xFFFFE8B8;
}
