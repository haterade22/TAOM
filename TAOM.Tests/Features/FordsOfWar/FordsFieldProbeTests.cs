using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Diagnostics;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Tests.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar;

[TestClass]
public class FordsFieldProbeTests
{
    private static readonly FordsMatchFacts Facts = new(FireRange: 60f, AttackRange: 12f, CaptureRadius: 20f, Lanes: 2, MaxLivePerSide: 100, MaxAlivePerSide: 3);

    private readonly MapLayout _layout = FordsTestLayout.Create();
    private readonly List<FordsUnitSample> _units = new();
    private readonly Dictionary<AgentKey, UnitRecord> _records = new();
    private readonly HashSet<string> _fallen = new();
    private readonly Dictionary<(MatchSide, int), LaneCommand> _applied = new();

    private void Unit(int key, MatchSide side, int lane, float x, float y, UnitRole role = UnitRole.Infantry)
    {
        var k = new AgentKey(key);
        _units.Add(new FordsUnitSample(k, side, lane, new Vec3f(x, y, 0f)));
        _records[k] = new UnitRecord(k, side, lane, 1, 10, role, null);
    }

    private static MatchView View(params MatchView.CapturePointView[] points) =>
        new(1, MatchSide.West, MatchPhase.Live, 100f, 0, 5, 3, new List<int> { 0, 1 }, new List<MatchView.LaneView>(),
            new List<MatchView.TowerView>(), new List<MatchView.HeroView>(), new List<MatchView.UnitOfferView>(),
            new List<MatchView.HeroOfferView>(), points, null);

    private FordsFieldSample Sample(MatchView? view = null, int backlog = 0) =>
        FordsFieldProbe.Sample(_units, _records, _layout, _fallen, _applied, view ?? View(), Facts, backlog);

    private static FordsLaneSample LaneOf(FordsFieldSample s, MatchSide side, int lane) => s.Lanes.Single(l => l.Side == side && l.Lane == lane);

    [TestMethod]
    public void Front_IsTheFarthestUnitFromItsOwnKeep()
    {
        Unit(1, MatchSide.West, 0, 100f, 25f);
        Unit(2, MatchSide.West, 0, 300f, 25f);
        Unit(3, MatchSide.East, 0, 900f, 25f);

        var sample = Sample(backlog: 7);

        Assert.AreEqual(260f, LaneOf(sample, MatchSide.West, 0).Front, 0.01f, "keep_west stands at x 40");
        Assert.AreEqual(60f, LaneOf(sample, MatchSide.East, 0).Front, 0.01f, "keep_east stands at x 960");
        Assert.AreEqual(2, LaneOf(sample, MatchSide.West, 0).Live);
        Assert.AreEqual(2, sample.LiveWest);
        Assert.AreEqual(1, sample.LiveEast);
        Assert.AreEqual(7, sample.Backlog);
        Assert.AreEqual(0, LaneOf(sample, MatchSide.West, 1).Live);
    }

    [TestMethod]
    public void HoldSettled_OnlyOnceTheLaneHasArrivedAtTheHoldPoint()
    {
        // tower_east_0_0 stands at x 640, fire range 60: x 600 is 40 m away, x 500 is 140 m away.
        Unit(1, MatchSide.West, 0, 500f, 0f);
        _applied[(MatchSide.West, 0)] = LaneCommand.HoldAt(new Vec3f(560f, 0f, 0f));

        var walking = LaneOf(Sample(), MatchSide.West, 0);
        Assert.IsFalse(walking.HoldSettled, "60 m short of the hold point, however long ago the order was given");
        Assert.AreEqual(-1f, walking.HoldFront, "no hold front before it settles");

        _units.Clear();
        Unit(1, MatchSide.West, 0, 552f, 0f);
        Unit(2, MatchSide.West, 0, 500f, 0f);
        var arrived = LaneOf(Sample(), MatchSide.West, 0);
        Assert.IsTrue(arrived.HoldSettled, "the front unit is 8 m from the hold point");
        Assert.AreEqual(88f, arrived.HoldFront, 0.01f);
    }

    [TestMethod]
    public void HoldSettled_NeedsTheFrontUnitAtTheHoldPoint_NotJustAFreshSpawnThere()
    {
        // The lane is walking back from a tower: a fresh spawn stands at the hold point but the front unit is 60 m beyond it.
        Unit(1, MatchSide.West, 0, 560f, 0f);
        Unit(2, MatchSide.West, 0, 620f, 0f);
        _applied[(MatchSide.West, 0)] = LaneCommand.HoldAt(new Vec3f(560f, 0f, 0f));

        var lane = LaneOf(Sample(), MatchSide.West, 0);

        Assert.IsFalse(lane.HoldSettled, "the front unit (x 620) is 60 m from the hold point");
    }

    [TestMethod]
    public void HoldSettled_AWideFrontWithItsMiddleAtTheHoldPoint_IsSettled()
    {
        // Lane 0 runs along y 0. The line's edge units stand 30 m to the side of the hold point, which the front rank spreads across.
        _applied[(MatchSide.West, 0)] = LaneCommand.HoldAt(new Vec3f(560f, 0f, 0f));
        Unit(1, MatchSide.West, 0, 560f, 0f);
        Unit(2, MatchSide.West, 0, 560f, -30f);
        Unit(3, MatchSide.West, 0, 560f, 30f);

        var lane = LaneOf(Sample(), MatchSide.West, 0);

        Assert.IsTrue(lane.HoldSettled, "measured along the lane axis, the front rank is on the hold point");
    }

    [TestMethod]
    public void HoldSettled_AFrontTwentyMetresShortAlongTheAxis_IsNotSettled()
    {
        _applied[(MatchSide.West, 0)] = LaneCommand.HoldAt(new Vec3f(560f, 0f, 0f));
        Unit(1, MatchSide.West, 0, 540f, 0f);
        Unit(2, MatchSide.West, 0, 540f, -30f);

        var lane = LaneOf(Sample(), MatchSide.West, 0);

        Assert.IsFalse(lane.HoldSettled);
    }

    [TestMethod]
    public void HoldSettled_ALaneAtTheHoldPointInsideFireRange_CountsItsUnitsInFire()
    {
        Unit(1, MatchSide.West, 0, 600f, 0f);
        Unit(2, MatchSide.West, 0, 500f, 0f);
        _applied[(MatchSide.West, 0)] = LaneCommand.HoldAt(new Vec3f(600f, 0f, 0f));

        var settled = LaneOf(Sample(), MatchSide.West, 0);

        Assert.IsTrue(settled.HoldSettled);
        Assert.AreEqual(1, settled.InFire);
        Assert.AreEqual(40f, settled.HoldFront, 0.01f);

        _fallen.Add("tower_east_0_0");
        _fallen.Add("tower_east_1_0");
        var afterFall = LaneOf(Sample(), MatchSide.West, 0);
        Assert.AreEqual(0, afterFall.InFire, "the inner towers (x 800) are out of range");
        Assert.AreEqual(200f, afterFall.HoldFront, 0.01f);
    }

    [TestMethod]
    public void HoldSettled_OnlyLaneOnesTowerCoversLaneZerosHoldPoint_TheSampleNamesIt()
    {
        // tower_east_0_0 (x 640, y 0) has fallen; tower_east_1_0 (x 640, y 50) is 54 m from the hold point and covers it; the
        // inner towers (x 800) are out of range.
        _fallen.Add(FordsTestLayout.EastOuter0);
        _applied[(MatchSide.West, 0)] = LaneCommand.HoldAt(new Vec3f(620f, 0f, 0f));
        Unit(1, MatchSide.West, 0, 620f, 0f);

        var lane = LaneOf(Sample(), MatchSide.West, 0);

        Assert.IsTrue(lane.HoldSettled);
        Assert.AreEqual(1, lane.InFire);
        Assert.AreEqual(FordsTestLayout.EastOuter1, lane.FireTower);
    }

    [TestMethod]
    public void FireTower_IsNullWhenNoStandingEnemyTowerCoversTheLane()
    {
        _applied[(MatchSide.West, 0)] = LaneCommand.HoldAt(new Vec3f(300f, 0f, 0f));
        Unit(1, MatchSide.West, 0, 300f, 0f);

        Assert.IsNull(LaneOf(Sample(), MatchSide.West, 0).FireTower);
    }

    [TestMethod]
    public void EnemyMeleeInRange_CountsOnlyOpposingMeleeRoles()
    {
        // tower_west_0_0 stands at x 360, attack range 12.
        Unit(1, MatchSide.East, 0, 365f, 0f, UnitRole.Infantry);
        Unit(2, MatchSide.East, 0, 365f, 0f, UnitRole.Spear);
        Unit(3, MatchSide.East, 0, 365f, 0f, UnitRole.Cavalry);
        Unit(4, MatchSide.East, 0, 365f, 0f, UnitRole.Ranged);
        Unit(5, MatchSide.East, 0, 365f, 0f, UnitRole.Hero);
        Unit(6, MatchSide.East, 0, 400f, 0f, UnitRole.Infantry);
        Unit(7, MatchSide.West, 0, 365f, 0f, UnitRole.Infantry);

        var sample = Sample();

        Assert.AreEqual(3, sample.Towers.Single(t => t.TowerId == FordsTestLayout.WestOuter0).EnemyMeleeInRange);
        Assert.AreEqual(0, sample.Towers.Single(t => t.TowerId == FordsTestLayout.EastOuter0).EnemyMeleeInRange);
        _fallen.Add(FordsTestLayout.WestOuter0);
        CollectionAssert.DoesNotContain(Sample().Towers.Select(t => t.TowerId).ToList(), FordsTestLayout.WestOuter0);
    }

    [TestMethod]
    public void Points_NearestApproach_AndContestedFromTheView()
    {
        var layout = _layout with { CapturePoints = new List<CapturePointSlot> { new("pt_a", new Vec3f(500f, 0f, 0f)), new("pt_b", new Vec3f(500f, 400f, 0f)) } };
        Unit(1, MatchSide.West, 0, 470f, 0f);
        Unit(2, MatchSide.East, 0, 520f, 0f);
        var view = View(new MatchView.CapturePointView("pt_a", null, MatchSide.West, 0.5f, true, true),
            new MatchView.CapturePointView("pt_b", null, null, 0f, false, false));

        var sample = FordsFieldProbe.Sample(_units, _records, layout, _fallen, _applied, view, Facts, 0);

        var a = sample.Points.Single(p => p.PointId == "pt_a");
        Assert.IsTrue(a.Contested);
        Assert.AreEqual(20f, a.NearestApproach, 0.01f);
        var b = sample.Points.Single(p => p.PointId == "pt_b");
        Assert.IsFalse(b.Contested);
        Assert.IsTrue(b.NearestApproach > 100f);
    }
}
