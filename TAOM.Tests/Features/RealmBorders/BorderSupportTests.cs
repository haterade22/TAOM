using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Core;
using TAOM.Features.RealmBorders.Domain;

namespace TAOM.Tests.Features.RealmBorders;

/// <summary>
/// The smaller pure pieces behind the borders: resampling for draping, the navmesh terrain mapping,
/// tile binning with change detection, map-mode styling, realm-name placement and the crossing
/// notice's memory.
/// </summary>
[TestClass]
public class BorderSupportTests
{
    // --- resampling ---

    [TestMethod]
    public void Resample_OpenLine_SpacesPointsEvenlyAndKeepsBothEnds()
    {
        var points = PolylineMath.Resample(new[] { new MapPoint(0, 0), new MapPoint(10, 0) }, spacing: 3f, closed: false);

        Assert.AreEqual(new MapPoint(0, 0), points.First());
        Assert.AreEqual(new MapPoint(10, 0), points.Last());
        Assert.IsTrue(points.Zip(points.Skip(1), (a, b) => (b - a).Length).All(d => d <= 3f + 1e-4f));
        Assert.AreEqual(5, points.Count, "ten units at no more than three apart: four gaps");
    }

    [TestMethod]
    public void Resample_ClosedRing_DoesNotRepeatItsStart()
    {
        var square = new[] { new MapPoint(0, 0), new MapPoint(4, 0), new MapPoint(4, 4), new MapPoint(0, 4) };

        var points = PolylineMath.Resample(square, spacing: 1f, closed: true);

        Assert.AreEqual(16, points.Count);
        Assert.AreNotEqual(points.First(), points.Last());
    }

    // --- terrain classification (TaleWorlds.Core.TerrainType codes, v1.5.3) ---

    /// <summary>
    /// Keyed by the engine's own enum, so a renumbered or added TerrainType fails here instead of
    /// silently turning that terrain into claimable open ground. Reading a TaleWorlds.Core enum is
    /// metadata only, so this runs on the hosted CI's reference assemblies (EnumNamesTests does too).
    /// </summary>
    private static readonly Dictionary<TerrainType, TerrainClass> ExpectedClasses = new Dictionary<TerrainType, TerrainClass>
    {
        [TerrainType.Water] = TerrainClass.Water,
        [TerrainType.Mountain] = TerrainClass.Wall,
        [TerrainType.Snow] = TerrainClass.Rough,
        [TerrainType.Steppe] = TerrainClass.Open,
        [TerrainType.Plain] = TerrainClass.Open,
        [TerrainType.Desert] = TerrainClass.Open,
        [TerrainType.Swamp] = TerrainClass.Rough,
        [TerrainType.Dune] = TerrainClass.Open,
        [TerrainType.Bridge] = TerrainClass.Crossing,
        [TerrainType.River] = TerrainClass.River,
        [TerrainType.Forest] = TerrainClass.Rough,
        [TerrainType.Fording] = TerrainClass.Crossing,
        [TerrainType.Lake] = TerrainClass.Water,
        [TerrainType.Canyon] = TerrainClass.Wall,
        [TerrainType.RuralArea] = TerrainClass.Open,
        [TerrainType.CoastalSea] = TerrainClass.Water,
        [TerrainType.OpenSea] = TerrainClass.Water,
        [TerrainType.Beach] = TerrainClass.Open,
        [TerrainType.Cliff] = TerrainClass.Wall,
        [TerrainType.NonNavigableRiver] = TerrainClass.River,
        [TerrainType.LandRestriction] = TerrainClass.Wall,
        [TerrainType.SeaRestriction] = TerrainClass.Water,
        [TerrainType.UnderBridge] = TerrainClass.River,
    };

    [TestMethod]
    public void Classify_EveryEngineTerrainType_MapsToItsClass()
    {
        var unlisted = Enum.GetValues(typeof(TerrainType)).Cast<TerrainType>()
            .Where(t => !ExpectedClasses.ContainsKey(t)).ToList();
        Assert.AreEqual(0, unlisted.Count, "TerrainType values this feature has not classified: " + string.Join(", ", unlisted));

        foreach (var pair in ExpectedClasses)
            Assert.AreEqual(pair.Value, TerrainClassifier.Classify((int)pair.Key), pair.Key.ToString());
    }

    [TestMethod]
    public void Classify_NotAnEngineValue_IsClaimableOpenGround()
    {
        Assert.AreEqual(TerrainClass.Open, TerrainClassifier.Classify(-7), "never a wall by accident");
    }

    // --- tiles ---

    private static BorderQuad Quad(float x, float y, uint colour = 0xFF112233)
    {
        BorderVertex V(float px, float py) => new BorderVertex(new MapPoint(px, py), colour, 0f, 0f);
        return new BorderQuad(V(x, y), V(x, y + 1), V(x + 1, y + 1), V(x + 1, y));
    }

    [TestMethod]
    public void Bin_QuadsFallIntoTheTileHoldingTheirCentre()
    {
        var tiles = TileBinner.Bin(new[] { Quad(10, 10), Quad(130, 10), Quad(127.6f, 5) }, tileSize: 128f);

        Assert.AreEqual(1, tiles[(0, 0)].Count, "the quad centred at 128.1 belongs to the second tile");
        Assert.AreEqual(2, tiles[(1, 0)].Count);
    }

    [TestMethod]
    public void ContentHash_SameQuads_Match_AndAColourChangeDoesNot()
    {
        var a = new List<BorderQuad> { Quad(1, 1), Quad(3, 3) };
        var b = new List<BorderQuad> { Quad(1, 1), Quad(3, 3) };
        var c = new List<BorderQuad> { Quad(1, 1), Quad(3, 3, 0xFFFF0000) };

        Assert.AreEqual(TileBinner.ContentHash(a), TileBinner.ContentHash(b));
        Assert.AreNotEqual(TileBinner.ContentHash(a), TileBinner.ContentHash(c));
    }

    // --- map modes ---

    private static readonly RealmBorderLine GondorMordor = new RealmBorderLine("empire_s", "empire_w", new LatticePoint[0], false);
    private static readonly RealmBorderLine GondorRohan = new RealmBorderLine("empire_w", "vlandia", new LatticePoint[0], false);

    private static uint Colour(string realm) => realm switch { "empire_s" => 0xFFB0231Bu, "empire_w" => 0xFF3F76B8u, _ => 0xFFD8A12Bu };

    [TestMethod]
    public void PaintFor_Political_UsesTheChosenLookAndRealmColours()
    {
        var paint = BorderStyleSelector.PaintFor(GondorMordor, MapMode.Political, heraldic: false, playerRealm: null, gildPlayerRealm: true, Colour);

        Assert.AreEqual(LineStyle.Atlas, paint.Style);
        Assert.AreEqual(0xFFB0231Bu, paint.LeftColour);
        Assert.AreEqual(0xFF3F76B8u, paint.RightColour);
        Assert.IsFalse(paint.Gilded);
    }

    [TestMethod]
    public void PaintFor_PlayersOwnFrontier_IsGilded()
    {
        Assert.IsTrue(BorderStyleSelector.PaintFor(GondorRohan, MapMode.Political, false, "vlandia", true, Colour).Gilded);
        Assert.IsFalse(BorderStyleSelector.PaintFor(GondorRohan, MapMode.Political, false, "vlandia", false, Colour).Gilded, "the setting turns it off");
    }

    [TestMethod]
    public void PaintFor_HeraldicSetting_UsesTheBands()
    {
        Assert.AreEqual(LineStyle.Heraldic, BorderStyleSelector.PaintFor(GondorRohan, MapMode.Political, true, null, true, Colour).Style);
    }

    private static RealmBorderLine Between(string left, string right) => new RealmBorderLine(left, right, new LatticePoint[0], false);

    [TestMethod]
    public void PaintFor_WarMode_BurnsWhereThePlayersSideMeetsAnEnemy()
    {
        Assert.AreEqual(LineStyle.WarFront, BorderStyleSelector.PaintFor(Between(RelationGroups.Own, RelationGroups.Enemy), MapMode.War, false, null, true, Colour).Style);
        Assert.AreEqual(LineStyle.WarFront, BorderStyleSelector.PaintFor(Between(RelationGroups.Enemy, RelationGroups.Ally), MapMode.War, false, null, true, Colour).Style);
    }

    [TestMethod]
    public void PaintFor_WarMode_OtherBordersKeepTheChosenLook()
    {
        Assert.AreEqual(LineStyle.Atlas, BorderStyleSelector.PaintFor(Between(RelationGroups.Enemy, RelationGroups.Neutral), MapMode.War, false, null, true, Colour).Style);
        Assert.AreEqual(LineStyle.Heraldic, BorderStyleSelector.PaintFor(Between(RelationGroups.Own, RelationGroups.Ally), MapMode.War, true, null, true, Colour).Style);
    }

    [TestMethod]
    public void PaintFor_WarMode_OnlyThePlayersOwnFrontIsEmphasised_AndNothingIsGilded()
    {
        var own = BorderStyleSelector.PaintFor(Between(RelationGroups.Own, RelationGroups.Enemy), MapMode.War, false, RelationGroups.Own, true, Colour);
        var allied = BorderStyleSelector.PaintFor(Between(RelationGroups.Ally, RelationGroups.Enemy), MapMode.War, false, RelationGroups.Own, true, Colour);

        Assert.IsTrue(own.Emphasised);
        Assert.IsFalse(allied.Emphasised);
        Assert.IsFalse(own.Gilded, "the gold cord is the political mode's");
    }

    [TestMethod]
    public void RelationGroups_Of_FollowsTheNameplateOrder()
    {
        Assert.AreEqual(RelationGroups.Neutral, RelationGroups.Of((RealmRelation)0));
        Assert.AreEqual(RelationGroups.Own, RelationGroups.Of((RealmRelation)1));
        Assert.AreEqual(RelationGroups.Enemy, RelationGroups.Of((RealmRelation)2));
        Assert.AreEqual(RelationGroups.Ally, RelationGroups.Of((RealmRelation)3));
        Assert.AreEqual(RelationGroups.Neutral, RelationGroups.Of((RealmRelation)9), "a value the engine may add later reads as neutral");
    }

    [TestMethod]
    public void Next_CyclesThroughEveryModeAndBack()
    {
        Assert.AreEqual(MapMode.Alignment, MapModes.Next(MapMode.Political));
        Assert.AreEqual(MapMode.War, MapModes.Next(MapMode.Alignment));
        Assert.AreEqual(MapMode.Political, MapModes.Next(MapMode.War));
    }

    // --- realm names ---

    [TestMethod]
    public void Place_TwoRealms_EachLabelSitsDeepInsideItsOwnLand()
    {
        var map = BoundaryTracerTests.Map(Enumerable.Repeat("00000000001111111111", 9).ToArray());

        var labels = RealmLabelPlacer.Place(map, new[] { "gondor", "mordor" }, minimumCells: 10);

        var gondor = labels.Single(l => l.Realm == "gondor");
        var mordor = labels.Single(l => l.Realm == "mordor");
        Assert.IsTrue(gondor.Position.X < 10f && mordor.Position.X > 10f);
        Assert.AreEqual(4.5f, gondor.Position.Y, 1.01f, "vertically central");
        Assert.AreEqual(90, gondor.Cells);
    }

    [TestMethod]
    public void Place_RealmInTwoPieces_IsLabelledOnItsLargerPiece()
    {
        var map = BoundaryTracerTests.Map("0011111222", "0011111222", "0011111222");

        var labels = RealmLabelPlacer.Place(map, new[] { "gondor", "rohan", "gondor" }, minimumCells: 1);

        var gondor = labels.Single(l => l.Realm == "gondor");
        Assert.IsTrue(gondor.Position.X >= 7f, "the east piece has 9 cells, the west one 6");
        Assert.AreEqual(9, gondor.Cells);
    }

    [TestMethod]
    public void Place_RealmTooSmall_GetsNoLabel()
    {
        var map = BoundaryTracerTests.Map("0000000001", "0000000000");

        Assert.IsFalse(RealmLabelPlacer.Place(map, new[] { "gondor", "rohan" }, minimumCells: 5).Any(l => l.Realm == "rohan"));
    }

    // --- crossing notices ---

    [TestMethod]
    public void Observe_FirstPosition_IsNeverAnnounced()
    {
        var tracker = new BorderCrossingTracker(cooldownHours: 12);

        Assert.IsNull(tracker.Observe("vlandia", 0));
    }

    [TestMethod]
    public void Observe_EnteringAnotherRealm_AnnouncesIt()
    {
        var tracker = new BorderCrossingTracker(12);
        tracker.Observe("vlandia", 0);

        Assert.AreEqual("empire_w", tracker.Observe("empire_w", 1));
        Assert.IsNull(tracker.Observe("empire_w", 2), "staying is not a crossing");
    }

    [TestMethod]
    public void Observe_WildLand_IsNotAnnounced()
    {
        var tracker = new BorderCrossingTracker(12);
        tracker.Observe("vlandia", 0);

        Assert.IsNull(tracker.Observe(null, 1));
        Assert.AreEqual("empire_w", tracker.Observe("empire_w", 2));
    }

    [TestMethod]
    public void Observe_RidingAlongABorder_DoesNotRepeatWithinTheCooldown()
    {
        var tracker = new BorderCrossingTracker(12);
        tracker.Observe("vlandia", 0);
        Assert.AreEqual("empire_w", tracker.Observe("empire_w", 1));
        tracker.Observe("vlandia", 2);

        Assert.IsNull(tracker.Observe("empire_w", 3), "back into Gondor two hours later");
        tracker.Observe("vlandia", 5);
        Assert.AreEqual("empire_w", tracker.Observe("empire_w", 14), "after the cooldown it is news again");
    }

    [TestMethod]
    public void Reset_ForgetsWhereThePartyWas()
    {
        var tracker = new BorderCrossingTracker(12);
        tracker.Observe("vlandia", 0);
        tracker.Reset();

        Assert.IsNull(tracker.Observe("empire_w", 1), "the first position after a reset is silent again");
    }
}
