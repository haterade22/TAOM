using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.RealmBorders.Domain;

namespace TAOM.Tests.Features.RealmBorders;

/// <summary>
/// Pins what each approved look puts on the map, in quads the renderer turns into meshes: the Atlas
/// look's watercolour on each side and dash-dot ink on the line, the heraldic bands with their gap,
/// the gold cord on the player's own frontier, and the war-front glow. The approved look is the
/// product; these keep a refactor from quietly changing it.
/// </summary>
[TestClass]
public class BorderPainterTests
{
    private const uint Gondor = 0xFF3F76B8, Mordor = 0xFFB0231B;
    private static readonly BorderLook Look = new BorderLook();

    private static List<BorderQuad> Paint(LinePaint paint, float length = 20f)
    {
        var quads = new List<BorderQuad>();
        BorderPainter.Paint(new[] { new MapPoint(0, 0), new MapPoint(length, 0) }, closed: false, paint, Look, quads);
        return quads;
    }

    private static float Along(BorderQuad q) => Math.Abs(q.NearEnd.Position.X - q.NearStart.Position.X);

    [TestMethod]
    public void Paint_Atlas_WashesEachSideInItsOwnRealmColour()
    {
        var quads = Paint(new LinePaint(LineStyle.Atlas, Gondor, Mordor, Gilded: false, Emphasised: false));

        Assert.IsTrue(quads.Any(q => (q.NearStart.Colour & 0xFFFFFF) == (Gondor & 0xFFFFFF) && q.FarStart.Position.Y > 0), "Gondor's wash lies left of the line");
        Assert.IsTrue(quads.Any(q => (q.NearStart.Colour & 0xFFFFFF) == (Mordor & 0xFFFFFF) && q.FarStart.Position.Y < 0), "Mordor's wash lies right of the line");
    }

    [TestMethod]
    public void Paint_AtlasWash_FadesFromTheLineIntoTheRealm()
    {
        var wash = Paint(new LinePaint(LineStyle.Atlas, Gondor, Mordor, false, false))
            .First(q => (q.NearStart.Colour & 0xFFFFFF) == (Gondor & 0xFFFFFF));

        Assert.IsTrue(wash.NearStart.Colour >> 24 > wash.FarStart.Colour >> 24, "alpha falls away from the border");
        Assert.AreEqual(0u, wash.FarStart.Colour >> 24, "the far edge is fully transparent");
    }

    [TestMethod]
    public void Paint_AtlasInk_IsBrokenIntoTheDashDotPattern()
    {
        var ink = Paint(new LinePaint(LineStyle.Atlas, Gondor, Mordor, false, false), length: 20f)
            .Where(q => q.NearStart.Colour == Look.InkColour).ToList();

        float period = Look.DashLength + Look.DotLength + 2 * Look.PatternGap;
        float onPerPeriod = Look.DashLength + Look.DotLength;
        float expected = 0f;
        for (float start = 0f; start < 20f; start += period)
        {
            expected += Math.Min(Look.DashLength, 20f - start);
            float dotStart = start + Look.DashLength + Look.PatternGap;
            if (dotStart < 20f)
                expected += Math.Min(Look.DotLength, 20f - dotStart);
        }
        Assert.AreEqual(expected, ink.Sum(Along), 1e-3, $"ink covers {onPerPeriod} of every {period} units");
        Assert.IsTrue(ink.Count > 1);
    }

    [TestMethod]
    public void Paint_Heraldic_LeavesAGapOnTheLine()
    {
        var quads = Paint(new LinePaint(LineStyle.Heraldic, Gondor, Mordor, false, false));

        float nearest = quads.Where(q => (q.NearStart.Colour & 0xFFFFFF) != (Look.KeylineColour & 0xFFFFFF))
            .Min(q => Math.Abs(q.NearStart.Position.Y));
        Assert.AreEqual(Look.BandGap / 2f, nearest, 1e-4, "each band starts half the gap away from the line");
    }

    [TestMethod]
    public void Paint_Gilded_AddsTheGoldCordOnTheLine()
    {
        var gilded = Paint(new LinePaint(LineStyle.Atlas, Gondor, Mordor, Gilded: true, Emphasised: false));
        var plain = Paint(new LinePaint(LineStyle.Atlas, Gondor, Mordor, Gilded: false, Emphasised: false));

        Assert.IsTrue(gilded.Any(q => q.NearStart.Colour == Look.GoldColour || q.FarStart.Colour == Look.GoldColour));
        Assert.IsFalse(plain.Any(q => q.NearStart.Colour == Look.GoldColour || q.FarStart.Colour == Look.GoldColour));
    }

    [TestMethod]
    public void Paint_WarFront_GlowsOnBothSides()
    {
        var quads = Paint(new LinePaint(LineStyle.WarFront, Gondor, Mordor, false, false));

        Assert.IsTrue(quads.Any(q => q.FarStart.Position.Y > 0) && quads.Any(q => q.FarStart.Position.Y < 0));
        Assert.IsTrue(quads.All(q => (q.NearStart.Colour & 0xFFFFFF) == (Look.EmberColour & 0xFFFFFF)
                                  || (q.NearStart.Colour & 0xFFFFFF) == (Look.EmberCoreColour & 0xFFFFFF)));
    }

    [TestMethod]
    public void Paint_EmphasisedWarFront_GlowsWider()
    {
        float Reach(List<BorderQuad> qs) => qs.Max(q => Math.Abs(q.FarStart.Position.Y));

        Assert.IsTrue(Reach(Paint(new LinePaint(LineStyle.WarFront, Gondor, Mordor, false, Emphasised: true)))
                      > Reach(Paint(new LinePaint(LineStyle.WarFront, Gondor, Mordor, false, Emphasised: false))));
    }

    [TestMethod]
    public void Paint_WidthScale_ScalesEveryBand()
    {
        var wide = new BorderLook { WidthScale = 2f };
        var quads = new List<BorderQuad>();
        BorderPainter.Paint(new[] { new MapPoint(0, 0), new MapPoint(10, 0) }, false, new LinePaint(LineStyle.Atlas, Gondor, Mordor, false, false), wide, quads);

        Assert.AreEqual(2f * Look.WashWidth, quads.Max(q => q.FarStart.Position.Y), 1e-3);
    }

    [TestMethod]
    public void Paint_DegenerateLine_PaintsNothing()
    {
        var quads = new List<BorderQuad>();
        BorderPainter.Paint(new[] { new MapPoint(1, 1) }, false, new LinePaint(LineStyle.Atlas, Gondor, Mordor, false, false), Look, quads);

        Assert.AreEqual(0, quads.Count);
    }
}
