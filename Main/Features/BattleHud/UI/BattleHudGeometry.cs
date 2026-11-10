using System;

namespace TAOM.Features.BattleHud.UI;

/// <summary>
/// Issue #1095: the circular battle HUD's layout in GUI units, free of TaleWorlds types so the ring
/// widget's math is unit-testable. Angles are degrees clockwise from 12 o'clock; offsets are from the
/// HUD centre with y pointing down (screen space). Every ring is a full circle from
/// <see cref="StartDegrees"/> to <see cref="EndDegrees"/>: its track closes the circle and its fill
/// covers the channel's ratio of it. Health is the outermost ring (slot 0); present channels pack inward
/// toward a fixed centre disk, so the full HUD is <see cref="Diameter"/> across (160, the smaller size
/// that replaced the 224 unit HUD judged too big in game) and one with fewer channels is smaller. The
/// widget draws these units times the engine's scale, so the markup box must match.
/// </summary>
public static class BattleHudGeometry
{
    public const float Diameter = 160f;
    public const float CentreRadius = 41.2f;
    public const float RimWidth = 1.1f;
    /// <summary>Band pitch is <see cref="BandThickness"/> plus <see cref="BandGap"/>, 11.5 units, so the
    /// outer edges stay at 53.8, 65.3 and 76.8. The gap equals <see cref="ChannelRimWidth"/>: neighbouring
    /// bands' gold rims coincide as one separator, and the innermost rim starts where the centre rim ends
    /// (42.3), so no dark ring shows between channels.</summary>
    public const float BandThickness = 10.7f;
    public const float BandGap = 0.8f;
    public const float OuterPad = 2.1f;

    /// <summary>Every ring starts at 12 o'clock and runs clockwise once round.</summary>
    public const float StartDegrees = 0f;
    public const float EndDegrees = 360f;

    /// <summary>A divisor of 360, so arc boundaries land on a fixed table of angles.</summary>
    public const float SegmentDegrees = 4.5f;

    /// <summary>Fine gold edge drawn just outside each channel band, along its sweep.</summary>
    public const float ChannelRimWidth = 0.8f;

    /// <summary>The gold leaf sprig (bh_leaf_sprig, cropped from cb_branch): its longer side in GUI units,
    /// its angle on the shell, and how much of its height overlaps the shell so the stem grows from it.</summary>
    public const float LeafSize = 26f;
    public const float LeafDegrees = 330f;
    public const float LeafRimOverlap = 0.3f;

    /// <summary>The one packing rule, shared by the mapper and the ring widget: health is slot 0 (the
    /// outermost band), then horse, then shield; an absent channel takes no slot. Returns the ring count
    /// and -1 for an absent channel's slot.</summary>
    public static int PackSlots(bool hasHorse, bool hasShield, out int horseSlot, out int shieldSlot)
    {
        var slot = 1;
        horseSlot = hasHorse ? slot++ : -1;
        shieldSlot = hasShield ? slot++ : -1;
        return slot;
    }

    public static float BandInnerRadius(int slot, int ringCount)
    {
        var fromInside = ringCount - 1 - slot;
        return CentreRadius + RimWidth + BandGap + fromInside * (BandThickness + BandGap);
    }

    public static float BandOuterRadius(int slot, int ringCount) => BandInnerRadius(slot, ringCount) + BandThickness;

    /// <summary>Radius of the outer gold rim's outside edge.</summary>
    public static float HudRadius(int ringCount)
    {
        var bands = ringCount > 0 ? BandOuterRadius(0, ringCount) : CentreRadius + RimWidth;
        return bands + OuterPad + RimWidth;
    }

    /// <summary>Radius of the sprig's centre for a sprig <paramref name="height"/> units tall, drawn
    /// tangent to the shell with its bottom (the stem) overlapping the rim.</summary>
    public static float LeafCentreRadius(int ringCount, float height)
        => HudRadius(ringCount) + height * (0.5f - LeafRimOverlap);

    /// <summary>The ammo capsule hung below the circle at 6 o'clock (bh_ammo_capsule, its width over height
    /// the exported art's 4.138). The icon is inset from its left end; the number fills the rest.</summary>
    public const float AmmoCapsuleWidth = 110f;
    public const float AmmoCapsuleHeight = 26.6f;
    public const float AmmoIconSize = 24f;
    public const float AmmoIconInset = 8f;
    public const float AmmoNumberWidth = 48f;

    /// <summary>How far the capsule's top tucks under the shell: the shell rim's width, so it never
    /// reaches the dark pad or the health ring.</summary>
    public const float AmmoRimUnderlap = RimWidth;

    /// <summary>The capsule's top, from the top of the <see cref="Diameter"/> unit root whose centre is
    /// the circle's: just under the shell of the circle actually drawn for <paramref name="ringCount"/>.</summary>
    public static float AmmoCapsuleTop(int ringCount) => Diameter * 0.5f + HudRadius(ringCount) - AmmoRimUnderlap;

    /// <summary>The number's horizontal offset from the capsule centre: the middle of the space right of
    /// the icon, or the capsule centre when the weapon has no approved icon.</summary>
    public static float AmmoNumberOffsetX(bool hasIcon)
    {
        if (!hasIcon) return 0f;
        var half = AmmoCapsuleWidth * 0.5f;
        var iconRight = -half + AmmoIconInset + AmmoIconSize;
        var areaRight = half - AmmoIconInset;
        return (iconRight + areaRight) * 0.5f;
    }

    /// <summary>Where a ring's fill ends: the channel's ratio of the full circle from 12 o'clock.</summary>
    public static float FillEndDegrees(float fill01) => StartDegrees + (EndDegrees - StartDegrees) * Clamp01(fill01);

    /// <summary>The stone strip's horizontal texture coordinate at an angle: once round the ring, by
    /// absolute angle, so the grain stays put as a fill grows or shrinks and neighbouring segments
    /// continue it.</summary>
    public static float BandTextureU(float degrees)
        => Clamp01((degrees - StartDegrees) / (EndDegrees - StartDegrees));

    public static (float X, float Y) Polar(float degrees, float radius)
    {
        var radians = degrees * Math.PI / 180.0;
        return ((float)(Math.Sin(radians) * radius), (float)(-Math.Cos(radians) * radius));
    }

    /// <summary>The career disk fills bottom-up: its filled part is the arc from this angle clockwise
    /// through 6 o'clock to 360 minus it, closed by the horizontal fill line.</summary>
    public static float CentreFillStartDegrees(float fill01)
        => (float)(Math.Acos(2f * Clamp01(fill01) - 1f) * 180.0 / Math.PI);

    /// <summary>The fill line's offset from the centre, y down: the bottom edge when empty, the top when full.</summary>
    public static float CentreFillLineY(float fill01, float radius) => radius - 2f * radius * Clamp01(fill01);

    /// <summary>Segments needed to draw a sweep at <see cref="SegmentDegrees"/> per segment.</summary>
    public static int SegmentCount(float sweepDegrees)
    {
        if (!(sweepDegrees > 0f)) return 0;
        return (int)Math.Ceiling(sweepDegrees / SegmentDegrees - 0.0001f);
    }

    // Positive-requirement form: NaN lands at 0 (NaN-gate rule).
    public static float Clamp01(float v)
    {
        if (!(v > 0f)) return 0f;
        return v > 1f ? 1f : v;
    }
}
