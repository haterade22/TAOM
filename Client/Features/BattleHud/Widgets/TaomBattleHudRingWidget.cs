extern alias SNV;
using System;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;
using TAOM.Features.BattleHud.UI;
using Vector2 = SNV::System.Numerics.Vector2;

namespace TAOM.Features.BattleHud.Widgets;

/// <summary>
/// Issue #1095: draws the circular battle HUD in one widget: a dark backing disk, the health, horse
/// and shield bands as full rings from 12 o'clock (fill for the channel's ratio, track for the rest),
/// each edged with fine gold rims, the gold shell and leaf sprig, and the career disk with its
/// bottom-up fill. No channel icons are drawn.
/// Bands and the career fill overlay the resident stone strip (<see cref="StoneTexture"/>) on their
/// white masks, each segment sampling its own slice of it. A colour vision <see cref="ColourMode"/>
/// (<see cref="BattleHudColourPalette"/>) draws the three bands flat in its palette instead; the
/// centre, rims, backing and leaf never change with it.
/// <para>
/// Every drawable is its own <see cref="Rectangle2D"/> built with <c>Rectangle2D.Create()</c>: local
/// position, scale, pivot and rotation in screen pixels, then <c>CalculateMatrixFrame</c> against an
/// invalid parent. The renderer draws the rectangle's cached matrix frame, not its public corner fields
/// (v1.5.6 IL: <c>DrawSprite</c> copies the rectangle into <c>ImageDrawObject.Create</c> unchanged), so a
/// rectangle copied from <see cref="Widget.AreaRect"/> would always cover the whole widget. Arcs are
/// runs of short tangent rectangles, one per <see cref="BattleHudGeometry.SegmentDegrees"/>; the
/// career fill is one disk under a scissor. About 900 rectangles a frame at most, no child widget per
/// segment, no texture made or copied for any state. Materials alpha-blend by default, so transparent
/// pixels stay clear.
/// </para>
/// Layout and packing are <see cref="BattleHudGeometry"/> (no TAOM service types, so the GauntletPreview
/// app compiles this file unchanged).
/// </summary>
public class TaomBattleHudRingWidget : Widget
{
    public const string LeafSprig = "bh_leaf_sprig";

    /// <summary>Soft-edged band mask (white, alpha ramps at its top and bottom rows); without it the
    /// bands use BlankWhiteSquare and keep hard edges.</summary>
    public const string BandMask = "bh_band_mask";

    /// <summary>The approved charcoal stone strip (1920 by 130), a resident front-end image that the
    /// loading window already registers: the bands and the career fill overlay it on their white masks,
    /// so no texture is loaded, copied or made for the HUD. Without it they draw flat.</summary>
    public const string StoneTexture = "ld_bar_stone";

    // The stone is dark charcoal and the shader multiplies it into the colour (two_dimensional_ui.rs:
    // RGB *= color_factor, then *= the overlay sample), so textured paints are lifted by this gain to
    // keep the channel colours readable. Icons and rims keep a factor of 1. Tuned by eye in the preview.
    private const float StoneGain = 7f;

    // Each band samples its own third of the strip's height, so the three rings do not repeat one grain.
    private const float StoneBandRows = 3f;

    // The career fill's static square crop of the strip, starting this far along it.
    private const float StoneCentreU = 0.42f;

    // Tangent segments are widened by this many pixels so neighbours overlap instead of leaving hairlines.
    private const float SegmentOverlapPixels = 0.6f;

    // The fine gold rims are solid BlankWhiteSquare (the band mask's soft rows would fade a rim only a
    // pixel or so thick) and overlap their neighbours further, so each segment's sampled end edge lies
    // under the next segment's solid middle and no dotted seam shows. Overlapping opaque gold adds nothing.
    private const float RimOverlapPixels = 1.5f;

    private static readonly Color Backing = new Color(0.07f, 0.055f, 0.043f, 0.78f);
    private static readonly Color Track = new Color(0.17f, 0.137f, 0.106f, 0.95f);
    private static readonly Color HealthRed = new Color(0.78f, 0.13f, 0.14f, 1f);
    private static readonly Color HorseSage = new Color(0.49f, 0.58f, 0.40f, 1f);
    private static readonly Color ShieldBlueGrey = new Color(0.42f, 0.51f, 0.60f, 1f);
    private static readonly Color Gold = new Color(0.82f, 0.68f, 0.38f, 1f);
    private static readonly Color CentreDark = new Color(0.09f, 0.07f, 0.05f, 0.96f);
    private static readonly Color AbilityReady = new Color(0.72f, 0.57f, 0.18f, 1f);   // #B8912F, as the old bar
    private static readonly Color AbilityActive = new Color(0.98f, 0.80f, 0.38f, 1f);  // #FACC61
    private static readonly Color AbilityCooldown = new Color(0.34f, 0.28f, 0.15f, 1f); // #574726

    // The sign LocalRotation (degrees) needs for a clockwise turn, measured once from the engine's own
    // corner output; NaN until measured.
    private static float _rotationPerClockwiseDegree = float.NaN;

    private float _healthFill;
    private float _horseFill;
    private float _shieldFill;
    private bool _hasHorse;
    private bool _hasShield;
    private bool _hasAbility;
    private float _abilityFill;
    private bool _isAbilityReady;
    private bool _isAbilityActive;
    private bool _isAbilityCooldown;
    private int _colourMode;

    public TaomBattleHudRingWidget(UIContext context) : base(context)
    {
    }

    [Editor(false)]
    public float HealthFill { get => _healthFill; set { if (_healthFill != value) { _healthFill = value; OnPropertyChanged(value, nameof(HealthFill)); } } }

    [Editor(false)]
    public float HorseFill { get => _horseFill; set { if (_horseFill != value) { _horseFill = value; OnPropertyChanged(value, nameof(HorseFill)); } } }

    [Editor(false)]
    public float ShieldFill { get => _shieldFill; set { if (_shieldFill != value) { _shieldFill = value; OnPropertyChanged(value, nameof(ShieldFill)); } } }

    [Editor(false)]
    public bool HasHorse { get => _hasHorse; set { if (_hasHorse != value) { _hasHorse = value; OnPropertyChanged(value, nameof(HasHorse)); } } }

    [Editor(false)]
    public bool HasShield { get => _hasShield; set { if (_hasShield != value) { _hasShield = value; OnPropertyChanged(value, nameof(HasShield)); } } }

    /// <summary>A <see cref="BattleHudColourMode"/> index (the Accessibility colour option, bound from the
    /// mixin); Default, or any unknown value, draws the stone rings unchanged.</summary>
    [Editor(false)]
    public int ColourMode { get => _colourMode; set { if (_colourMode != value) { _colourMode = value; OnPropertyChanged(value, nameof(ColourMode)); } } }

    [Editor(false)]
    public bool HasAbility { get => _hasAbility; set { if (_hasAbility != value) { _hasAbility = value; OnPropertyChanged(value, nameof(HasAbility)); } } }

    [Editor(false)]
    public float AbilityFill { get => _abilityFill; set { if (_abilityFill != value) { _abilityFill = value; OnPropertyChanged(value, nameof(AbilityFill)); } } }

    [Editor(false)]
    public bool IsAbilityReady { get => _isAbilityReady; set { if (_isAbilityReady != value) { _isAbilityReady = value; OnPropertyChanged(value, nameof(IsAbilityReady)); } } }

    [Editor(false)]
    public bool IsAbilityActive { get => _isAbilityActive; set { if (_isAbilityActive != value) { _isAbilityActive = value; OnPropertyChanged(value, nameof(IsAbilityActive)); } } }

    [Editor(false)]
    public bool IsAbilityCooldown { get => _isAbilityCooldown; set { if (_isAbilityCooldown != value) { _isAbilityCooldown = value; OnPropertyChanged(value, nameof(IsAbilityCooldown)); } } }

    protected override void OnRender(TwoDimensionContext twoDimensionContext, TwoDimensionDrawContext drawContext)
    {
        base.OnRender(twoDimensionContext, drawContext);
        if (!(Size.X > 0f) || !(Size.Y > 0f))
            return;

        // Looked up every frame (a dictionary read each): the bh_* images are registered by the battle
        // HUD hold and can be released and re-registered under the same names while this widget lives.
        var sprites = Context.SpriteData;
        var square = sprites.GetSprite("BlankWhiteSquare");
        var circle = sprites.GetSprite("BlankWhiteCircle");
        if (square == null || circle == null)
            return;
        var band = sprites.GetSprite(BandMask) ?? square;
        var stone = sprites.GetSprite(StoneTexture);
        if (stone != null && (stone.Texture == null || stone.Width <= 0 || stone.Height <= 0))
            stone = null;

        // The widget's on-screen centre: AreaRect's corners already carry the parent's offsets and the
        // AgentStatus slide. Every rectangle below is placed in the same screen pixels.
        var area = AreaRect;
        var centre = (area.TopLeft + area.BottomRight) * 0.5f;
        var unit = _scaleToUse;
        var alpha = AlphaFactor * Context.ContextAlpha;
        var ringCount = BattleHudGeometry.PackSlots(HasHorse, HasShield, out var horseSlot, out var shieldSlot);
        var hudRadius = BattleHudGeometry.HudRadius(ringCount) * unit;
        var rim = BattleHudGeometry.RimWidth * unit;
        var gold = new Paint(square, Material(drawContext, square, Gold, alpha));

        Box(drawContext, new Paint(circle, Material(drawContext, circle, Backing, alpha)), centre, 2f * (hudRadius - rim), 2f * (hudRadius - rim), 0f);
        Arc(drawContext, gold, centre, hudRadius - rim, hudRadius, 0f, 360f, RimOverlapPixels);

        // A colour vision palette draws the three bands flat (no stone, so no StoneGain): solid fills on a
        // dark solid track, for contrast that does not depend on the grain. Default keeps the stone paints.
        var palette = BattleHudColourPalette.Accessible(BattleHudColourPalette.Resolve(ColourMode));
        var bandStone = palette.HasValue ? null : stone;
        var track = palette.HasValue ? Opaque(palette.Value.Track) : Track;
        var health = palette.HasValue ? Opaque(palette.Value.Health) : HealthRed;
        var horse = palette.HasValue ? Opaque(palette.Value.Horse) : HorseSage;
        var shield = palette.HasValue ? Opaque(palette.Value.Shield) : ShieldBlueGrey;

        Band(drawContext, band, bandStone, alpha, gold, health, track, centre, unit, 0, ringCount, HealthFill);
        if (horseSlot >= 0)
            Band(drawContext, band, bandStone, alpha, gold, horse, track, centre, unit, horseSlot, ringCount, HorseFill);
        if (shieldSlot >= 0)
            Band(drawContext, band, bandStone, alpha, gold, shield, track, centre, unit, shieldSlot, ringCount, ShieldFill);

        var centreRadius = BattleHudGeometry.CentreRadius * unit;
        Box(drawContext, new Paint(circle, Material(drawContext, circle, CentreDark, alpha)), centre, 2f * centreRadius, 2f * centreRadius, 0f);
        if (HasAbility)
            CentreFill(drawContext, Textured(drawContext, circle, stone, AbilityColour(), alpha, 0f, 1f), stone, centre, centreRadius);
        Arc(drawContext, gold, centre, centreRadius, centreRadius + rim, 0f, 360f, RimOverlapPixels);

        Leaf(drawContext, sprites.GetSprite(LeafSprig), alpha, centre, unit, ringCount);
    }

    private Color AbilityColour()
        => IsAbilityActive ? AbilityActive : IsAbilityCooldown ? AbilityCooldown : AbilityReady;

    private static Color Opaque(uint rgb)
        => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);

    // A channel: a full ring from 12 o'clock, its stone fill covering the channel's ratio clockwise and
    // its track the rest, edged inside and out by a fine gold rim all the way round. Fill and track sample
    // the same strip rows by absolute angle, so the grain runs on unbroken across the fill end and stays
    // put as it moves. With no stone both draw flat on the band mask.
    private void Band(TwoDimensionDrawContext dc, Sprite mask, Sprite? stone, float alpha, Paint gold, Color colour, Color track, Vector2 centre, float unit, int slot, int ringCount, float fill01)
    {
        var inner = BattleHudGeometry.BandInnerRadius(slot, ringCount) * unit;
        var outer = BattleHudGeometry.BandOuterRadius(slot, ringCount) * unit;
        var rim = BattleHudGeometry.ChannelRimWidth * unit;
        var start = BattleHudGeometry.StartDegrees;
        var stop = BattleHudGeometry.EndDegrees;
        var end = BattleHudGeometry.FillEndDegrees(fill01);
        var v0 = slot / StoneBandRows;
        var v1 = (slot + 1) / StoneBandRows;
        Arc(dc, Textured(dc, mask, stone, colour, alpha, v0, v1), centre, inner, outer, start, end, SegmentOverlapPixels);
        Arc(dc, Textured(dc, mask, stone, track, alpha, v0, v1), centre, inner, outer, end, stop, SegmentOverlapPixels);
        Arc(dc, gold, centre, outer, outer + rim, start, stop, RimOverlapPixels);
        Arc(dc, gold, centre, inner - rim, inner, start, stop, RimOverlapPixels);
    }

    // The career disk fills from the bottom: the whole disk drawn under a scissor that starts at the fill
    // line, overlaid with a fixed square crop of the stone (never the whole strip squeezed into the disk).
    private void CentreFill(TwoDimensionDrawContext dc, Paint paint, Sprite? stone, Vector2 centre, float radius)
    {
        var fill = BattleHudGeometry.Clamp01(AbilityFill);
        if (!(fill > 0f))
            return;
        var cropWidth = stone != null ? (float)stone.Height / stone.Width : 1f;
        var material = MaterialFor(dc, paint, 2f * radius, 2f * radius, StoneCentreU, StoneCentreU + cropWidth);
        var lineY = centre.Y + BattleHudGeometry.CentreFillLineY(fill, radius);
        var bottom = centre.Y + radius;
        var clip = Frame(new Vector2(centre.X, (lineY + bottom) * 0.5f), 2f * radius, bottom - lineY, 0f);
        dc.PushScissor(in clip);
        try
        {
            Draw(dc, paint.Sprite, material, centre, 2f * radius, 2f * radius, 0f);
        }
        finally
        {
            dc.PopScissor();
        }
    }

    // An annular arc as tangent rectangles, one per table step: each sits at the step's middle angle,
    // its width the outer chord plus overlapPixels, its height the ring thickness, its top edge outward.
    // The sprite's vertical axis therefore runs radially, so the band mask softens a band's two edges.
    // A textured paint samples, per segment, only the strip slice between the step's two absolute angles.
    private void Arc(TwoDimensionDrawContext dc, Paint paint, Vector2 centre, float inner, float outer, float from, float to, float overlapPixels)
    {
        var thickness = outer - inner;
        if (!(to > from) || !(thickness > 0f))
            return;
        var middleRadius = (inner + outer) * 0.5f;
        var a = from;
        while (a < to)
        {
            var next = ((float)Math.Floor(a / BattleHudGeometry.SegmentDegrees + 0.0001f) + 1f) * BattleHudGeometry.SegmentDegrees;
            if (next > to)
                next = to;
            var middle = (a + next) * 0.5f;
            var chord = 2f * outer * (float)Math.Sin((next - a) * Math.PI / 360.0) + overlapPixels;
            var (x, y) = BattleHudGeometry.Polar(middle, middleRadius);
            var material = MaterialFor(dc, paint, chord, thickness, BattleHudGeometry.BandTextureU(a), BattleHudGeometry.BandTextureU(next));
            Draw(dc, paint.Sprite, material, centre + new Vector2(x, y), chord, thickness, middle);
            a = next;
        }
    }

    // A band or fill paint: the stone overlaid on the white mask when the stone is registered, else flat.
    private static Paint Textured(TwoDimensionDrawContext dc, Sprite mask, Sprite? stone, Color colour, float alpha, float v0, float v1)
        => stone == null
            ? new Paint(mask, Material(dc, mask, colour, alpha))
            : new Paint(mask, stone.Texture, colour, alpha, v0, v1);

    // A flat paint's shared material, or for a stone paint a fresh one per queued rectangle showing the
    // strip window (u0, v0) to (u1, v1). The overlay maps the rectangle's 0..1 base UV through
    // clamp((uv + offset) / scale), offset and scale being these values over the rectangle's pixel size,
    // so scale is width / (u1 - u0) and offset u0 times it. A material is never changed after it is queued.
    private static SimpleMaterial MaterialFor(TwoDimensionDrawContext dc, in Paint paint, float width, float height, float u0, float u1)
    {
        if (paint.Stone == null)
            return paint.Flat!;
        var uSpan = u1 - u0 > 0.0001f ? u1 - u0 : 0.0001f;
        var vSpan = paint.V1 - paint.V0 > 0.0001f ? paint.V1 - paint.V0 : 0.0001f;
        var material = Material(dc, paint.Sprite, paint.Colour, paint.Alpha);
        material.ColorFactor = StoneGain;
        material.OverlayEnabled = true;
        material.UseOverlayAlphaAsMask = true;
        material.OverlayTexture = paint.Stone;
        material.OverlayTextureWidth = width / uSpan;
        material.OverlayTextureHeight = height / vSpan;
        material.OverlayXOffset = u0 * material.OverlayTextureWidth;
        material.OverlayYOffset = paint.V0 * material.OverlayTextureHeight;
        // Compatibility contract: the game's platform (TwoDimensionEnginePlatform.DrawImage) reads only the
        // pixel width, height and offsets above and ignores StartCoordinate and Size; the standalone
        // GauntletPreview (DirectXGraphicsContext.DrawImageAux) reads only these two. They carry the same
        // window, normalized, so the preview shader can sample it. Neither changes the game's draw.
        material.StartCoordinate = new Vector2(u0, paint.V0);
        material.Size = new Vector2(uSpan, vSpan);
        return material;
    }

    // The gold leaf sprig, tangent to the shell at its upper left: rotated by its angle so its x axis runs
    // clockwise along the rim and its top points outward; its bottom edge (the stem) overlaps the rim.
    private void Leaf(TwoDimensionDrawContext dc, Sprite? sprite, float alpha, Vector2 centre, float unit, int ringCount)
    {
        if (sprite == null || sprite.Width <= 0 || sprite.Height <= 0)
            return;
        var (width, height) = Fit(sprite, BattleHudGeometry.LeafSize * unit);
        var (x, y) = BattleHudGeometry.Polar(BattleHudGeometry.LeafDegrees, BattleHudGeometry.LeafCentreRadius(ringCount, height / unit) * unit);
        Box(dc, new Paint(sprite, Material(dc, sprite, Color.White, alpha)), centre + new Vector2(x, y), width, height, BattleHudGeometry.LeafDegrees);
    }

    private static (float Width, float Height) Fit(Sprite sprite, float longest)
    {
        var aspect = (float)sprite.Width / sprite.Height;
        return aspect >= 1f ? (longest, longest / aspect) : (longest * aspect, longest);
    }

    private void Box(TwoDimensionDrawContext dc, Paint paint, Vector2 middle, float width, float height, float clockwiseDegrees)
        => Draw(dc, paint.Sprite, MaterialFor(dc, paint, width, height, 0f, 1f), middle, width, height, clockwiseDegrees);

    private void Draw(TwoDimensionDrawContext dc, Sprite sprite, SimpleMaterial material, Vector2 middle, float width, float height, float clockwiseDegrees)
    {
        if (!(width > 0f) || !(height > 0f))
            return;
        var rect = Frame(middle, width, height, clockwiseDegrees);
        dc.DrawSprite(sprite, material, in rect, _scaleToUse);
    }

    /// <summary>A fresh screen-space rectangle centred on <paramref name="middle"/>, turned clockwise.
    /// Its cached matrix frame is built by the engine from these local values; nothing is copied from
    /// the widget's own rectangle.</summary>
    private static Rectangle2D Frame(Vector2 middle, float width, float height, float clockwiseDegrees)
    {
        var size = new Vector2(width, height);
        var rect = Rectangle2D.Create();
        rect.LocalPosition = middle - size * 0.5f;
        rect.LocalScale = size;
        rect.LocalPivot = new Vector2(0.5f, 0.5f);
        rect.LocalRotation = clockwiseDegrees == 0f ? 0f : clockwiseDegrees * RotationPerClockwiseDegree();
        var screen = default(Rectangle2D); // IsValid false: no parent transform, screen pixels
        rect.CalculateMatrixFrame(in screen);
        // How the engine applies the pivot is not documented; whatever it does, put the rectangle's
        // actual centre (from the corners it just computed out of the same frame) back on the target.
        var drift = middle - (rect.TopLeft + rect.BottomRight) * 0.5f;
        if (drift.LengthSquared() > 0.0001f)
        {
            rect.LocalPosition += drift;
            rect.CalculateMatrixFrame(in screen);
        }
        rect.CalculateVisualMatrixFrame();
        return rect;
    }

    /// <summary>The LocalRotation value per clockwise screen degree: +1 or -1. The unit is degrees
    /// (installed v1.5.6 RectangleHelper.CreateMatrixFrame); only the sign is measured, once, by turning a
    /// 10 by 10 test rectangle 30 degrees each way and keeping the sign whose top edge comes out pointing
    /// 30 degrees clockwise of screen right.</summary>
    private static float RotationPerClockwiseDegree()
    {
        if (!float.IsNaN(_rotationPerClockwiseDegree))
            return _rotationPerClockwiseDegree;

        foreach (var candidate in new[] { 1f, -1f })
        {
            var rect = Rectangle2D.Create();
            rect.LocalPosition = Vector2.Zero;
            rect.LocalScale = new Vector2(10f, 10f);
            rect.LocalPivot = new Vector2(0.5f, 0.5f);
            rect.LocalRotation = 30f * candidate;
            var screen = default(Rectangle2D);
            rect.CalculateMatrixFrame(in screen);
            var edge = rect.TopRight - rect.TopLeft;
            if (Math.Abs(edge.X - 8.660f) < 0.05f && Math.Abs(edge.Y - 5f) < 0.05f)
                return _rotationPerClockwiseDegree = candidate;
        }
        return _rotationPerClockwiseDegree = 1f;
    }

    // One material per colour per frame, shared by all of that colour's rectangles: the queued draws
    // read the material when the frame is flushed, which is safe only because its values never change
    // after this. Blending is read-only on SimpleMaterial and true by default (v1.5.6), so a fresh
    // material already alpha-blends and transparent sprite pixels stay clear.
    private static SimpleMaterial Material(TwoDimensionDrawContext dc, Sprite sprite, Color color, float alpha)
    {
        var material = dc.CreateSimpleMaterial();
        material.OverlayEnabled = false;
        material.CircularMaskingEnabled = false;
        material.Texture = sprite.Texture;
        // Raw drawable rectangles never nine-slice: a sprite's border (the preview's BlankWhiteSquare has
        // 1 px on every side) would exceed a 1.1 px rim and break it into dots.
        material.NinePatchParameters = default(SpriteNinePatchParameters);
        material.Color = color;
        material.ColorFactor = 1f;
        material.AlphaFactor = alpha;
        material.HueFactor = 0f;
        material.SaturationFactor = 0f;
        material.ValueFactor = 0f;
        return material;
    }

    // Flat: one shared material. Stone: the mask, the overlay texture, its colour and the strip rows it
    // samples; MaterialFor builds its material per rectangle.
    private readonly struct Paint
    {
        public Paint(Sprite sprite, SimpleMaterial material)
        {
            Sprite = sprite;
            Flat = material;
            Stone = null;
            Colour = default;
            Alpha = 0f;
            V0 = 0f;
            V1 = 1f;
        }

        public Paint(Sprite sprite, Texture stone, Color colour, float alpha, float v0, float v1)
        {
            Sprite = sprite;
            Flat = null;
            Stone = stone;
            Colour = colour;
            Alpha = alpha;
            V0 = v0;
            V1 = v1;
        }

        public Sprite Sprite { get; }
        public SimpleMaterial? Flat { get; }
        public Texture? Stone { get; }
        public Color Colour { get; }
        public float Alpha { get; }
        public float V0 { get; }
        public float V1 { get; }
    }
}
