using System;
using System.Globalization;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BattleHud.UI;
using TAOM.Features.CareerSystem.UI;

namespace TAOM.Tests.Features.BattleHud;

// Issue #1095: the circular HUD's geometry. Health is the outermost ring; present channels pack inward
// toward a fixed centre disk, so the full HUD is 160 GUI units across (the smaller size that replaced
// the 224 unit HUD judged too big in game) and a HUD with fewer channels is smaller without an empty
// ring. Every ring is a complete circle from 12 o'clock clockwise: its track always closes the circle,
// and its fill covers the channel's ratio of it. No channel icons sit on the rings. Neighbouring rings
// sit against each other, parted only by their shared gold rim.
[TestClass]
public class BattleHudGeometryTests
{
    private const float ProposedDiameter = 160f;

    // ── Size ─────────────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void HudRadius_ThreeRings_IsTheProposed160UnitsAcross()
    {
        Assert.AreEqual(ProposedDiameter, 2f * BattleHudGeometry.HudRadius(3), 0.01f);
        Assert.AreEqual(ProposedDiameter, BattleHudGeometry.Diameter, 0.001f);
    }

    [TestMethod]
    public void HudRadius_FewerRings_IsSmaller()
    {
        Assert.IsTrue(BattleHudGeometry.HudRadius(1) < BattleHudGeometry.HudRadius(2));
        Assert.IsTrue(BattleHudGeometry.HudRadius(2) < BattleHudGeometry.HudRadius(3));
    }

    [TestMethod]
    public void BandThickness_AtTheSmallerDiameter_StaysReadable()
    {
        Assert.IsTrue(BattleHudGeometry.BandThickness >= 8f, $"band {BattleHudGeometry.BandThickness} too thin to read");
    }

    [TestMethod]
    public void LeafSize_ScalesWithTheDiameter()
    {
        // The approved sprig was 36 units on the 224 unit HUD.
        var ratio = BattleHudGeometry.LeafSize / BattleHudGeometry.Diameter;
        Assert.AreEqual(36f / 224f, ratio, 0.1f * 36f / 224f, $"leaf {BattleHudGeometry.LeafSize} on a {BattleHudGeometry.Diameter} HUD");
    }

    [TestMethod]
    public void BandRadii_ThreeRings_DoNotOverlapAndClearTheCentre()
    {
        var previousInner = float.MaxValue;
        for (var slot = 0; slot < 3; slot++)
        {
            var outer = BattleHudGeometry.BandOuterRadius(slot, 3);
            var inner = BattleHudGeometry.BandInnerRadius(slot, 3);
            Assert.AreEqual(BattleHudGeometry.BandThickness, outer - inner, 0.001f);
            Assert.IsTrue(outer < previousInner, $"slot {slot} overlaps the band outside it");
            previousInner = inner;
        }
        Assert.IsTrue(previousInner > BattleHudGeometry.CentreRadius + BattleHudGeometry.RimWidth);
    }

    // Rims are drawn at [outer, outer + w] and [inner - w, inner] (TaomBattleHudRingWidget.Band), the
    // centre rim at [CentreRadius, CentreRadius + RimWidth]. Neighbouring rings sit against each other as
    // in the mockup: their gold rims meet or overlap into one narrow separator, with no dark band between.
    private const float Touch = 0.01f;

    [DataTestMethod]
    [DataRow(2)]
    [DataRow(3)]
    public void ChannelRims_NeighbouringBands_MeetWithNoDarkGap(int ringCount)
    {
        var w = BattleHudGeometry.ChannelRimWidth;
        var pairs = 0;
        for (var slot = 0; slot < ringCount - 1; slot++)
        {
            var innerRimOfOuterBandStart = BattleHudGeometry.BandInnerRadius(slot, ringCount) - w;
            var outerRimOfInnerBandEnd = BattleHudGeometry.BandOuterRadius(slot + 1, ringCount) + w;
            Assert.IsTrue(innerRimOfOuterBandStart <= outerRimOfInnerBandEnd + Touch,
                $"{ringCount} rings: a {innerRimOfOuterBandStart - outerRimOfInnerBandEnd:F2} unit dark gap between slots {slot} and {slot + 1}");
            Assert.IsTrue(innerRimOfOuterBandStart >= BattleHudGeometry.BandOuterRadius(slot + 1, ringCount) - Touch,
                $"{ringCount} rings: slot {slot}'s rim covers slot {slot + 1}'s colour");
            Assert.IsTrue(outerRimOfInnerBandEnd <= BattleHudGeometry.BandInnerRadius(slot, ringCount) + Touch,
                $"{ringCount} rings: slot {slot + 1}'s rim covers slot {slot}'s colour");
            pairs++;
        }
        Assert.AreEqual(ringCount - 1, pairs, "every neighbouring pair checked");
    }

    [DataTestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public void ChannelRims_InnermostBand_MeetsTheCentreRimWithoutCoveringTheDisk(int ringCount)
    {
        var innermost = ringCount - 1;
        var innerRimStart = BattleHudGeometry.BandInnerRadius(innermost, ringCount) - BattleHudGeometry.ChannelRimWidth;
        var centreRimEnd = BattleHudGeometry.CentreRadius + BattleHudGeometry.RimWidth;

        Assert.IsTrue(innerRimStart <= centreRimEnd + Touch,
            $"{ringCount} rings: a {innerRimStart - centreRimEnd:F2} unit dark gap between the innermost rim and the centre rim");
        Assert.IsTrue(innerRimStart >= BattleHudGeometry.CentreRadius - Touch,
            $"{ringCount} rings: the innermost channel rim covers the career disk");
        Assert.IsTrue(BattleHudGeometry.BandInnerRadius(innermost, ringCount) >= centreRimEnd - Touch,
            $"{ringCount} rings: the innermost band's colour covers the centre rim");
    }

    [TestMethod]
    public void ChannelRims_OutermostBand_KeepsItsDarkGapToTheShell()
    {
        Assert.IsTrue(BattleHudGeometry.HudRadius(3) - BattleHudGeometry.RimWidth
            - (BattleHudGeometry.BandOuterRadius(0, 3) + BattleHudGeometry.ChannelRimWidth) >= 1f, "outermost rim touches the shell");
    }

    // The approved band edges, as literals: tightening the rings must not move a ring's outside edge or
    // the 160 unit HUD bounds.
    [DataTestMethod]
    [DataRow(0, 3, 76.8f)]
    [DataRow(1, 3, 65.3f)]
    [DataRow(2, 3, 53.8f)]
    [DataRow(0, 2, 65.3f)]
    [DataRow(1, 2, 53.8f)]
    [DataRow(0, 1, 53.8f)]
    public void BandOuterRadius_EverySlot_KeepsTheApprovedEdge(int slot, int ringCount, float expected)
    {
        Assert.AreEqual(expected, BattleHudGeometry.BandOuterRadius(slot, ringCount), Touch);
    }

    [TestMethod]
    public void HudRadius_ThreeRings_IsEightyUnits()
    {
        Assert.AreEqual(80f, BattleHudGeometry.HudRadius(3), Touch);
    }

    [TestMethod]
    public void LeafCentre_SitsOnTheUpperLeftOfTheShellAndOverlapsIt()
    {
        var radius = BattleHudGeometry.LeafCentreRadius(3, BattleHudGeometry.LeafSize);
        var (x, y) = BattleHudGeometry.Polar(BattleHudGeometry.LeafDegrees, radius);

        Assert.IsTrue(x < 0f && y < 0f, "upper left");
        Assert.IsTrue(radius - BattleHudGeometry.LeafSize * 0.5f < BattleHudGeometry.HudRadius(3), "the stem reaches the rim");
        Assert.IsTrue(radius > BattleHudGeometry.HudRadius(3), "the leaves stand outside the rim");
    }

    [TestMethod]
    public void BandOuterRadius_HealthOnly_SitsNextToTheCentre()
    {
        var expected = BattleHudGeometry.CentreRadius + BattleHudGeometry.RimWidth + BattleHudGeometry.BandGap
            + BattleHudGeometry.BandThickness;
        Assert.AreEqual(expected, BattleHudGeometry.BandOuterRadius(0, 1), 0.001f);
    }

    // ── Packing: health only, horse only, shield only, both; never an empty slot ───────────────

    [DataTestMethod]
    [DataRow(false, false, 1, -1, -1)]
    [DataRow(true, false, 2, 1, -1)]
    [DataRow(false, true, 2, -1, 1)]
    [DataRow(true, true, 3, 1, 2)]
    public void PackSlots_EveryEquipmentMix_PacksPresentChannelsWithNoEmptySlot(bool horse, bool shield, int rings, int horseSlot, int shieldSlot)
    {
        Assert.AreEqual(rings, BattleHudGeometry.PackSlots(horse, shield, out var h, out var s));
        Assert.AreEqual(horseSlot, h);
        Assert.AreEqual(shieldSlot, s);
    }

    // ── Full circles: the track closes the ring, the fill covers the ratio ─────────────────────

    [TestMethod]
    public void Ring_StartsAtTwelveAndClosesTheFullCircle()
    {
        Assert.AreEqual(0f, BattleHudGeometry.StartDegrees, 0.001f, "12 o'clock");
        Assert.AreEqual(360f, BattleHudGeometry.EndDegrees - BattleHudGeometry.StartDegrees, 0.001f, "no gap left in the ring");
    }

    [DataTestMethod]
    [DataRow(0f, 0f)]
    [DataRow(0.25f, 90f)]
    [DataRow(0.5f, 180f)]
    [DataRow(1f, 360f)]
    [DataRow(1.5f, 360f)]
    [DataRow(-0.2f, 0f)]
    [DataRow(float.NaN, 0f)]
    public void FillEndDegrees_MapsTheRatioAroundTheWholeRing(float fill, float expected)
    {
        Assert.AreEqual(expected, BattleHudGeometry.FillEndDegrees(fill), 0.001f);
    }

    [TestMethod]
    public void BandTextureU_RunsOnceAroundTheRingAndEachSegmentSamplesASmallSlice()
    {
        // U is a function of the absolute angle only, so the grain stays put as a fill changes, and each
        // tangent segment samples [U(a), U(b)] of the strip instead of repeating the whole texture.
        Assert.AreEqual(0f, BattleHudGeometry.BandTextureU(BattleHudGeometry.StartDegrees), 0.001f);
        Assert.AreEqual(1f, BattleHudGeometry.BandTextureU(BattleHudGeometry.EndDegrees), 0.001f);
        var previous = BattleHudGeometry.BandTextureU(BattleHudGeometry.StartDegrees);
        for (var a = BattleHudGeometry.StartDegrees + BattleHudGeometry.SegmentDegrees; a <= BattleHudGeometry.EndDegrees + 0.001f; a += BattleHudGeometry.SegmentDegrees)
        {
            var u = BattleHudGeometry.BandTextureU(a);
            Assert.IsTrue(u > previous, $"U must grow clockwise: {previous} then {u} at {a} degrees");
            Assert.IsTrue(u - previous <= 0.05f, $"one segment samples {u - previous:P0} of the strip at {a} degrees");
            previous = u;
        }
    }

    // ── Shared math ────────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Polar_Top_PointsUpInScreenSpace()
    {
        var p = BattleHudGeometry.Polar(0f, 10f);

        Assert.AreEqual(0f, p.X, 0.001f);
        Assert.AreEqual(-10f, p.Y, 0.001f);
    }

    [TestMethod]
    public void Polar_Ninety_PointsRight()
    {
        var p = BattleHudGeometry.Polar(90f, 10f);

        Assert.AreEqual(10f, p.X, 0.001f);
        Assert.AreEqual(0f, p.Y, 0.001f);
    }

    [DataTestMethod]
    [DataRow(0f, 180f)]
    [DataRow(0.5f, 90f)]
    [DataRow(1f, 0f)]
    [DataRow(float.NaN, 180f)]
    public void CentreFillStartDegrees_FillsFromTheBottom(float fill, float expected)
    {
        Assert.AreEqual(expected, BattleHudGeometry.CentreFillStartDegrees(fill), 0.01f);
    }

    [DataTestMethod]
    [DataRow(0f, 50f)]
    [DataRow(0.5f, 0f)]
    [DataRow(1f, -50f)]
    public void CentreFillLineY_RisesWithFill(float fill, float expected)
    {
        Assert.AreEqual(expected, BattleHudGeometry.CentreFillLineY(fill, 50f), 0.001f);
    }

    [DataTestMethod]
    [DataRow(360f, 80)]
    [DataRow(4.5f, 1)]
    [DataRow(1f, 1)]
    [DataRow(0f, 0)]
    [DataRow(-5f, 0)]
    [DataRow(float.NaN, 0)]
    public void SegmentCount_CoversTheSweepAtTheFixedStep(float sweep, int expected)
    {
        Assert.AreEqual(expected, BattleHudGeometry.SegmentCount(sweep));
    }

    // ── The injected markup matches the geometry ───────────────────────────────────────────────
    // The ring widget draws in GUI units times the engine's scale, never from its own Width, so the
    // markup box, the career glyph and the key text must be sized to the same geometry by hand.

    private static XmlElement HudMarkup() => new CareerEnergyBarPrefab().GetDocument().DocumentElement!;

    private static float Number(XmlElement element, string attribute)
    {
        var text = element.GetAttribute(attribute);
        return text.Length == 0 ? 0f : float.Parse(text, CultureInfo.InvariantCulture);
    }

    private static XmlElement Child(string xpath)
    {
        var element = (XmlElement?)HudMarkup().SelectSingleNode(xpath);
        Assert.IsNotNull(element, xpath);
        return element!;
    }

    private static XmlElement Glyph() => Child("Children/Widget[@Sprite='@CareerGlyphSprite']");

    private static XmlElement Key() => Child("Children/TextWidget[@Text='@ActivationKeyText']");

    // The farthest corner of a centred box from the HUD centre.
    private static float FarCorner(XmlElement box)
    {
        var halfWidth = Number(box, "SuggestedWidth") * 0.5f;
        var halfHeight = Number(box, "SuggestedHeight") * 0.5f;
        var y = Math.Abs(Number(box, "PositionYOffset")) + halfHeight;
        return (float)Math.Sqrt(halfWidth * halfWidth + y * y);
    }

    [TestMethod]
    public void Markup_Root_IsTheGeometryDiameterAndKeepsItsCornerMargins()
    {
        var root = HudMarkup();

        Assert.AreEqual(ProposedDiameter, Number(root, "SuggestedWidth"), 0.001f);
        Assert.AreEqual(ProposedDiameter, Number(root, "SuggestedHeight"), 0.001f);
        Assert.AreEqual(2f * BattleHudGeometry.HudRadius(3), Number(root, "SuggestedWidth"), 0.01f, "markup and drawing must agree");
        Assert.AreEqual(28f, Number(root, "MarginRight"), 0.001f, "the right margin is kept");
        Assert.AreEqual(28f, Number(root, "MarginBottom"), 0.001f, "the bottom margin is kept");
    }

    [TestMethod]
    public void Markup_GlyphAndKey_FitInsideTheCentreDiskWithoutTouching()
    {
        var glyph = Glyph();
        var key = Key();

        Assert.IsTrue(FarCorner(glyph) <= BattleHudGeometry.CentreRadius, $"glyph corner {FarCorner(glyph):F1} outside the centre disk {BattleHudGeometry.CentreRadius}");
        Assert.IsTrue(FarCorner(key) <= BattleHudGeometry.CentreRadius, $"key corner {FarCorner(key):F1} outside the centre disk {BattleHudGeometry.CentreRadius}");
        var glyphBottom = Number(glyph, "PositionYOffset") + Number(glyph, "SuggestedHeight") * 0.5f;
        var keyTop = Number(key, "PositionYOffset") - Number(key, "SuggestedHeight") * 0.5f;
        Assert.IsTrue(glyphBottom <= keyTop, $"glyph bottom {glyphBottom} overlaps key top {keyTop}");
        Assert.IsTrue(Number(key, "SuggestedHeight") >= Number(key, "Brush.FontSize"), "the key box must hold its font");
    }

    [TestMethod]
    public void Markup_GlyphAndKeyFont_ScaleWithTheCentreDisk()
    {
        // Approved on the 224 unit HUD: a 44 unit glyph and a 22 point key on a 116 unit centre disk.
        var disk = 2f * BattleHudGeometry.CentreRadius;
        var glyphRatio = Number(Glyph(), "SuggestedWidth") / disk;
        var font = Number(Key(), "Brush.FontSize");

        Assert.AreEqual(44f / 116f, glyphRatio, 0.15f * 44f / 116f, $"glyph {Number(Glyph(), "SuggestedWidth")} on a {disk} disk");
        Assert.AreEqual(22f / 116f, font / disk, 0.15f * 22f / 116f, $"key font {font} on a {disk} disk");
        Assert.IsTrue(font >= 14f, $"key font {font} too small to read");
    }
}
