using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar;

[TestClass]
public class FordsTagLayoutReaderTests
{
    private static readonly CampDefaults Camp = new("hill_troll", 2, 250);

    private static FordsMarker Marker(string tag, float x, float y, float yaw = 0f) => new(tag, new Vec3f(x, y, 1f), yaw);

    private static List<FordsMarker> TwoLanes()
    {
        var markers = new List<FordsMarker>();
        for (var lane = 0; lane < 2; lane++)
        {
            for (var n = 0; n < 4; n++)
                markers.Add(Marker($"fow_lane_{lane}_{n}", n * 100f, lane * 50f));
            markers.Add(Marker($"fow_tower_west_{lane}_0", 120f, lane * 50f, 0f));
            markers.Add(Marker($"fow_tower_west_{lane}_1", 60f, lane * 50f, 0f));
            markers.Add(Marker($"fow_tower_east_{lane}_0", 180f, lane * 50f, 3.14f));
            markers.Add(Marker($"fow_tower_east_{lane}_1", 240f, lane * 50f, 3.14f));
        }

        markers.Add(Marker("fow_keep_west", 10f, 25f));
        markers.Add(Marker("fow_keep_east", 290f, 25f, 3.14f));
        markers.Add(Marker("fow_capture_0", 150f, 25f));
        markers.Add(Marker("fow_camp_0", 105f, 25f));
        return markers;
    }

    private static bool Read(IEnumerable<FordsMarker> markers, out MapLayout layout, out string problem) =>
        FordsTagLayoutReader.TryRead("greybox", markers, Camp, out layout, out problem);

    [TestMethod]
    public void TryRead_CompleteMarkers_BuildsTheLayoutWithDomainIds()
    {
        Assert.IsTrue(Read(TwoLanes(), out var layout, out var problem), problem);

        Assert.AreEqual("greybox", layout.SceneName);
        Assert.AreEqual(2, layout.Lanes.Count);
        Assert.AreEqual(4, layout.Lanes[1].Path.Count);
        Assert.AreEqual(10, layout.Towers.Count);
        var outer = layout.Towers.Single(t => t.Id == "tower_east_1_0");
        Assert.AreEqual(MatchSide.East, outer.Owner);
        Assert.AreEqual(1, outer.Lane);
        Assert.AreEqual(0, outer.Order);
        Assert.AreEqual(TowerKind.Tower, outer.Kind);
        Assert.AreEqual(3.14f, outer.Yaw);
        var keep = layout.Towers.Single(t => t.Id == "keep_west");
        Assert.AreEqual(TowerKind.Keep, keep.Kind);
        Assert.AreEqual(-1, keep.Lane);
        Assert.AreEqual(99, keep.Order);
    }

    [TestMethod]
    public void TryRead_CapturePointAndCamp_GetTheirIdsAndTheMapDefaults()
    {
        Assert.IsTrue(Read(TwoLanes(), out var layout, out _));

        Assert.AreEqual("point_0", layout.CapturePoints.Single().Id);
        var camp = layout.Camps.Single();
        Assert.AreEqual("camp_0", camp.Id);
        Assert.AreEqual("hill_troll", camp.TroopId);
        Assert.AreEqual(2, camp.Count);
        Assert.AreEqual(250, camp.Reward);
    }

    [TestMethod]
    public void TryRead_LanePointsOutOfOrder_AreSortedByTheirNumber()
    {
        var markers = TwoLanes();
        markers.Reverse();

        Assert.IsTrue(Read(markers, out var layout, out _));

        CollectionAssert.AreEqual(new[] { 0f, 100f, 200f, 300f }, layout.Lanes[0].Path.Select(p => p.X).ToArray());
    }

    [TestMethod]
    public void TryRead_UnrelatedAndMalformedTags_AreIgnored()
    {
        var markers = TwoLanes();
        markers.Add(Marker("tree", 5f, 5f));
        markers.Add(Marker("fow_lane_x_1", 5f, 5f));
        markers.Add(Marker("fow_tower_north_0_0", 5f, 5f));
        markers.Add(Marker("fow_unknown", 5f, 5f));

        Assert.IsTrue(Read(markers, out var layout, out _));

        Assert.AreEqual(10, layout.Towers.Count);
    }

    [TestMethod]
    public void TryRead_NoMarkers_ReportsNoLanes()
    {
        Assert.IsFalse(Read(new List<FordsMarker>(), out _, out var problem));

        StringAssert.Contains(problem, "lane");
    }

    [TestMethod]
    public void TryRead_MissingKeep_ReportsTheKeep()
    {
        var markers = TwoLanes().Where(m => m.Tag != "fow_keep_east").ToList();

        Assert.IsFalse(Read(markers, out _, out var problem));

        StringAssert.Contains(problem, "fow_keep_east");
    }

    [TestMethod]
    public void TryRead_LaneNumbersWithAGap_ReportsTheGap()
    {
        var markers = TwoLanes().Where(m => !m.Tag.StartsWith("fow_lane_1_") && !m.Tag.Contains("_1_")).ToList();
        markers.Add(Marker("fow_lane_2_0", 0f, 100f));
        markers.Add(Marker("fow_lane_2_1", 100f, 100f));

        Assert.IsFalse(Read(markers, out _, out var problem));

        StringAssert.Contains(problem, "lane 1");
    }

    [TestMethod]
    public void TryRead_LaneWithOnePoint_ReportsTheLane()
    {
        var markers = TwoLanes().Where(m => m.Tag != "fow_lane_1_1" && m.Tag != "fow_lane_1_2" && m.Tag != "fow_lane_1_3").ToList();

        Assert.IsFalse(Read(markers, out _, out var problem));

        StringAssert.Contains(problem, "lane 1");
    }

    [TestMethod]
    public void TryRead_LaneWithoutAnOuterTowerForASide_ReportsIt()
    {
        var markers = TwoLanes().Where(m => m.Tag != "fow_tower_east_0_0").ToList();

        Assert.IsFalse(Read(markers, out _, out var problem));

        StringAssert.Contains(problem, "tower_east_0_0");
    }

    [TestMethod]
    public void TryRead_MoreThanFourLanes_Reports()
    {
        var markers = new List<FordsMarker>();
        for (var lane = 0; lane < 5; lane++)
        {
            markers.Add(Marker($"fow_lane_{lane}_0", 0f, lane * 50f));
            markers.Add(Marker($"fow_lane_{lane}_1", 100f, lane * 50f));
            markers.Add(Marker($"fow_tower_west_{lane}_0", 30f, lane * 50f));
            markers.Add(Marker($"fow_tower_east_{lane}_0", 70f, lane * 50f));
        }

        markers.Add(Marker("fow_keep_west", 0f, 0f));
        markers.Add(Marker("fow_keep_east", 100f, 0f));

        Assert.IsFalse(Read(markers, out _, out var problem));

        StringAssert.Contains(problem, "4");
    }

    [TestMethod]
    public void TryRead_TowerOnALaneTheSceneDoesNotHave_ReportsTheTag()
    {
        var markers = TwoLanes();
        markers.Add(Marker("fow_tower_west_2_0", 60f, 100f));

        Assert.IsFalse(Read(markers, out _, out var problem));

        StringAssert.Contains(problem, "fow_tower_west_2_0");
    }

    [TestMethod]
    public void TryRead_TowerOnALaneNumberTooLongForAnInt_IsRejectedToo()
    {
        var markers = TwoLanes();
        markers.Add(Marker("fow_tower_east_99999999999_0", 60f, 100f));

        Assert.IsFalse(Read(markers, out _, out var problem));

        StringAssert.Contains(problem, "fow_tower_east_");
    }
}
