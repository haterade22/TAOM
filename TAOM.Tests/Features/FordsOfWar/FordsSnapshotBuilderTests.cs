using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar;

[TestClass]
public class FordsSnapshotBuilderTests
{
    private static readonly FordsTowerConfig Towers = FordsMatchConfig.Default.Towers;
    private static readonly FordsCaptureConfig Capture = FordsMatchConfig.Default.Capture;

    private static FordsUnitSample Sample(int key, MatchSide side, int lane, float x, float y) =>
        new(new AgentKey(key), side, lane, new Vec3f(x, y, 0f));

    private static MatchSnapshot Build(IReadOnlyList<FordsUnitSample> samples,
        IReadOnlyList<TowerSlot>? towers = null, IReadOnlyList<CapturePointSlot>? points = null, int heroKey = -1) =>
        FordsSnapshotBuilder.Build(0.5f, samples, key => key.Value == heroKey,
            towers ?? new List<TowerSlot>(), points ?? new List<CapturePointSlot>(), Towers, Capture);

    private static TowerSlot WestTower(float x, float y) =>
        new("tower_west_0_0", MatchSide.West, 0, 0, TowerKind.Tower, new Vec3f(x, y, 0f), 0f);

    [TestMethod]
    public void Build_TwoUnitsInALane_ReportsCountAndCenter()
    {
        var snapshot = Build(new[] { Sample(1, MatchSide.West, 0, 0f, 0f), Sample(2, MatchSide.West, 0, 10f, 0f) });

        var lane = snapshot.Lanes.Single();
        Assert.AreEqual(2, lane.LiveUnits);
        Assert.AreEqual(new Vec3f(5f, 0f, 0f), lane.Center);
        Assert.AreEqual(0.5f, snapshot.Dt);
    }

    [TestMethod]
    public void Build_EnemyAcrossTheLane_ReportsNearestEnemyDistanceFromTheCenter()
    {
        var snapshot = Build(new[]
        {
            Sample(1, MatchSide.West, 0, 0f, 0f), Sample(2, MatchSide.West, 0, 10f, 0f),
            Sample(3, MatchSide.East, 1, 30f, 0f), Sample(4, MatchSide.East, 1, 90f, 0f),
        });

        var west = snapshot.Lanes.Single(l => l.Side == MatchSide.West);
        Assert.AreEqual(25f, west.NearestEnemyDistance, 0.001f);
    }

    [TestMethod]
    public void Build_NoEnemyAnywhere_ReportsMaxValueDistance()
    {
        var snapshot = Build(new[] { Sample(1, MatchSide.West, 0, 0f, 0f) });

        Assert.AreEqual(float.MaxValue, snapshot.Lanes.Single().NearestEnemyDistance);
    }

    [TestMethod]
    public void Build_NoSamples_ReportsNoLanesAndEmptyCounts()
    {
        var snapshot = Build(new List<FordsUnitSample>());

        Assert.AreEqual(0, snapshot.Lanes.Count);
        Assert.AreEqual(0, snapshot.TowerTargets.Count);
        Assert.AreEqual(0, snapshot.LiveBySide[MatchSide.West]);
        Assert.AreEqual(0, snapshot.LiveBySide[MatchSide.East]);
    }

    [TestMethod]
    public void Build_Samples_CountLiveUnitsPerSide()
    {
        var snapshot = Build(new[]
        {
            Sample(1, MatchSide.West, 0, 0f, 0f), Sample(2, MatchSide.West, 1, 0f, 5f), Sample(3, MatchSide.East, 0, 50f, 0f),
        });

        Assert.AreEqual(2, snapshot.LiveBySide[MatchSide.West]);
        Assert.AreEqual(1, snapshot.LiveBySide[MatchSide.East]);
    }

    [TestMethod]
    public void Build_EnemyInFireRange_ListsItAsATowerTargetWithItsDistance()
    {
        var snapshot = Build(new[] { Sample(3, MatchSide.East, 0, 30f, 0f) }, new[] { WestTower(0f, 0f) });

        var targets = snapshot.TowerTargets.Single();
        Assert.AreEqual("tower_west_0_0", targets.TowerId);
        Assert.AreEqual(new AgentKey(3), targets.EnemiesInRange.Single().Key);
        Assert.AreEqual(30f, targets.EnemiesInRange.Single().Distance, 0.001f);
    }

    [TestMethod]
    public void Build_EnemyBeyondFireRangeOrFriend_IsNotATowerTarget()
    {
        var snapshot = Build(new[]
        {
            Sample(3, MatchSide.East, 0, Towers.FireRange + 5f, 0f),
            Sample(4, MatchSide.West, 0, 5f, 0f),
        }, new[] { WestTower(0f, 0f) });

        Assert.AreEqual(0, snapshot.TowerTargets.Count);
    }

    [TestMethod]
    public void Build_HeroInRange_IsFlaggedAsAHeroCandidate()
    {
        var snapshot = Build(new[] { Sample(3, MatchSide.East, 0, 20f, 0f), Sample(4, MatchSide.East, 0, 25f, 0f) },
            new[] { WestTower(0f, 0f) }, heroKey: 4);

        var candidates = snapshot.TowerTargets.Single().EnemiesInRange;
        Assert.IsFalse(candidates.Single(c => c.Key.Value == 3).IsHero);
        Assert.IsTrue(candidates.Single(c => c.Key.Value == 4).IsHero);
    }

    [TestMethod]
    public void Build_UnitsAroundACapturePoint_CountsEachSideInsideTheRadius()
    {
        var point = new CapturePointSlot("point_0", new Vec3f(100f, 0f, 0f));
        var snapshot = Build(new[]
        {
            Sample(1, MatchSide.West, 0, 95f, 0f), Sample(2, MatchSide.West, 0, 100f, 11f), Sample(3, MatchSide.West, 0, 100f, 13f),
            Sample(4, MatchSide.East, 0, 105f, 0f),
        }, points: new[] { point });

        var counts = snapshot.Capture.Single();
        Assert.AreEqual("point_0", counts.PointId);
        Assert.AreEqual(2, counts.West);
        Assert.AreEqual(1, counts.East);
    }

    [TestMethod]
    public void Build_UnitWithAnUnreadablePosition_CountsAtNoCapturePoint()
    {
        var point = new CapturePointSlot("point_0", new Vec3f(100f, 0f, 0f));
        var snapshot = Build(new[]
        {
            Sample(1, MatchSide.West, 0, float.NaN, 0f), Sample(2, MatchSide.East, 0, 100f, float.NaN), Sample(3, MatchSide.West, 0, 95f, 0f),
        }, points: new[] { point });

        var counts = snapshot.Capture.Single();
        Assert.AreEqual(1, counts.West);
        Assert.AreEqual(0, counts.East);
    }
}
