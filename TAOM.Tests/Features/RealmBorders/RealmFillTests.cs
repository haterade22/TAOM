using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.RealmBorders.Domain;
using static TAOM.Tests.Features.RealmBorders.BoundaryTracerTests;

namespace TAOM.Tests.Features.RealmBorders;

/// <summary>
/// The tint between the borders: each realm's land in its own colour, fading out beside another realm,
/// wild land or water so the border's wash carries the edge, and never blending two realms' colours.
/// </summary>
[TestClass]
public class RealmFillTests
{
    private const uint Red = 0xFFE8402A, Blue = 0xFF3F76B8;
    private static readonly Dictionary<string, uint> Colours = new Dictionary<string, uint> { ["west"] = Red, ["east"] = Blue };

    /// <summary>A 20 x 12 map: province 0 (west) in columns 0-9, province 1 (east) in 10-19, rows written north first.</summary>
    private static ProvinceMap TwoRealms() =>
        Map(Enumerable.Repeat(new string('0', 10) + new string('1', 10), 12).ToArray());

    private static List<BorderQuad> Paint(ProvinceMap map, IReadOnlyDictionary<string, uint> colours, float alpha = 0.3f, params string?[] groups)
    {
        var quads = new List<BorderQuad>();
        RealmFill.Paint(map, groups.Length == 0 ? new[] { "west", "east" } : groups, colours, alpha, quads);
        return quads;
    }

    private static IEnumerable<BorderVertex> Vertices(IEnumerable<BorderQuad> quads) =>
        quads.SelectMany(q => new[] { q.NearStart, q.FarStart, q.FarEnd, q.NearEnd });

    private static uint Alpha(BorderVertex v) => v.Colour >> 24;

    [TestMethod]
    public void Paint_TwoRealms_TintsEachInteriorInItsOwnColour()
    {
        var vertices = Vertices(Paint(TwoRealms(), Colours)).ToList();

        Assert.IsTrue(vertices.Any(v => v.Colour == BorderPainter.WithAlpha(Red, 0.3f) && v.Position.X < 10f));
        Assert.IsTrue(vertices.Any(v => v.Colour == BorderPainter.WithAlpha(Blue, 0.3f) && v.Position.X > 10f));
    }

    [TestMethod]
    public void Paint_BesideAnotherRealm_FadesToNothing()
    {
        var vertices = Vertices(Paint(TwoRealms(), Colours)).ToList();

        Assert.IsTrue(vertices.Where(v => System.Math.Abs(v.Position.X - 10f) < 1f).All(v => Alpha(v) == 0),
            "the cells either side of the border carry no tint, so the border's wash carries the edge");
        Assert.IsTrue(vertices.Where(v => v.Position.X < 10f).All(v => (v.Colour & 0xFFFFFF) == (Red & 0xFFFFFF)),
            "the west's tint never takes the east's colour, even where it fades");
    }

    [TestMethod]
    public void Paint_WildLandAndWater_StayUntinted()
    {
        var map = Map(Enumerable.Repeat(new string('0', 10) + new string('.', 10), 12).ToArray());

        var vertices = Vertices(Paint(map, Colours)).ToList();

        Assert.IsTrue(vertices.Count > 0);
        Assert.IsTrue(vertices.Where(v => v.Position.X > 10.5f).All(v => Alpha(v) == 0));
    }

    [TestMethod]
    public void Paint_GroupWithoutAColour_StaysUntinted()
    {
        var westOnly = new Dictionary<string, uint> { ["west"] = Red };

        var vertices = Vertices(Paint(TwoRealms(), westOnly)).ToList();

        Assert.IsFalse(vertices.Any(v => (v.Colour & 0xFFFFFF) == (Blue & 0xFFFFFF)), "the neutral land of the war map is left clear");
    }

    [TestMethod]
    public void Paint_NoStrength_PaintsNothing()
    {
        Assert.AreEqual(0, Paint(TwoRealms(), Colours, alpha: 0f).Count);
        Assert.AreEqual(0, Paint(TwoRealms(), Colours, alpha: float.NaN).Count);
    }

    [TestMethod]
    public void Paint_EveryQuad_HasOneColourSoFadingCornersLeaveNoDarkFringe()
    {
        foreach (var quad in Paint(TwoRealms(), Colours))
        {
            var colours = new[] { quad.NearStart, quad.FarStart, quad.FarEnd, quad.NearEnd }.Select(v => v.Colour & 0xFFFFFF).Distinct().ToList();
            Assert.AreEqual(1, colours.Count, "a corner fading to nothing keeps the quad's colour instead of black");
        }
    }

    [TestMethod]
    public void Paint_DeepInsideARealm_ReachesFullStrength()
    {
        var vertices = Vertices(Paint(TwoRealms(), Colours, alpha: 0.5f)).ToList();

        Assert.AreEqual((uint)System.Math.Round(0.5f * 255f), vertices.Max(Alpha));
    }
}
