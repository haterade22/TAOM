using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar;

[TestClass]
public class FordsSpawnPlacementTests
{
    private static LaneLayout StraightLane() =>
        new(0, Enumerable.Range(0, 11).Select(i => new Vec3f(100f + i * 100f, 50f, 0f)).ToList());

    [TestMethod]
    public void Place_WestFirstUnit_StartsNearTheWestEndFacingEast()
    {
        var spot = FordsSpawnPlacement.Place(StraightLane(), MatchSide.West, 0);

        Assert.IsTrue(spot.Position.X > 100f && spot.Position.X < 110f);
        Assert.AreEqual(1f, spot.Facing.X, 0.001f);
    }

    [TestMethod]
    public void Place_EastFirstUnit_StartsNearTheEastEndFacingWest()
    {
        var spot = FordsSpawnPlacement.Place(StraightLane(), MatchSide.East, 0);

        Assert.IsTrue(spot.Position.X < 1100f && spot.Position.X > 1090f);
        Assert.AreEqual(-1f, spot.Facing.X, 0.001f);
    }

    [TestMethod]
    public void Place_ManyRanks_GiveDistinctSpots()
    {
        var lane = StraightLane();
        var spots = Enumerable.Range(0, 48).Select(rank => FordsSpawnPlacement.Place(lane, MatchSide.West, rank).Position).ToList();

        Assert.AreEqual(48, spots.Distinct().Count());
    }

    [TestMethod]
    public void Place_RankBeyondTheBlock_WrapsInsteadOfWalkingOffTheLane()
    {
        var lane = StraightLane();

        var wrapped = FordsSpawnPlacement.Place(lane, MatchSide.West, 48);

        Assert.AreEqual(FordsSpawnPlacement.Place(lane, MatchSide.West, 0), wrapped);
    }

    [TestMethod]
    public void Place_EmptyPath_ReturnsOriginFacingTheEnemy()
    {
        var spot = FordsSpawnPlacement.Place(new LaneLayout(0, new List<Vec3f>()), MatchSide.East, 3);

        Assert.AreEqual(default, spot.Position);
        Assert.AreEqual(-1f, spot.Facing.X);
    }
}
