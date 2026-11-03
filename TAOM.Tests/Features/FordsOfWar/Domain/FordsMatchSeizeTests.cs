using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;
using static TAOM.Tests.Features.FordsOfWar.Domain.FordsMatchFixture;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>
/// The Seize order: the lane walks to its nearest open camp or capture point, fights there until its side holds it, then the
/// match returns it to Push. Test map: lanes along X at y = 0, 50 and 100, West base x=0, East base x=1000.
/// </summary>
[TestClass]
public class FordsMatchSeizeTests
{
    private static readonly Vec3f CampAt = new(300f, 25f, 0f);
    private static readonly Vec3f PointAt = new(700f, 25f, 0f);

    private static CampSlot Camp(string id = "camp_0", Vec3f? at = null, int count = 2) =>
        new(id, at ?? CampAt, "wolf", count, 250);

    private static CapturePointSlot Point(string id = "point_0", Vec3f? at = null) => new(id, at ?? PointAt);

    private static FordsMatch Match(IReadOnlyList<CampSlot>? camps = null, IReadOnlyList<CapturePointSlot>? points = null)
    {
        var layout = FordsTestLayout.Create();
        var lanes = layout.Lanes.ToList();
        lanes.Add(new LaneLayout(2, Enumerable.Range(0, 11).Select(i => new Vec3f(i * 100f, 100f, 0f)).ToList()));
        var map = layout with
        {
            Lanes = lanes,
            Camps = camps?.ToList() ?? new List<CampSlot>(),
            CapturePoints = points?.ToList() ?? new List<CapturePointSlot>(),
        };
        var slots = new[]
        {
            new CommanderSlot(WestSlot, MatchSide.West, true, null, new[] { 0, 1, 2 }),
            new CommanderSlot(EastSlot, MatchSide.East, false, "hard", new[] { 0, 1, 2 }),
        };
        return new FordsMatch(Config(), map, Factions(), slots, new SeededFordsRandom(7));
    }

    private static LaneSnapshot Lane(MatchSide side, int lane, float x, int count = 10, float y = 0f)
    {
        var positions = Enumerable.Range(0, count).Select(_ => new Vec3f(x, y, 0f)).ToList();
        return new LaneSnapshot(side, lane, count, new Vec3f(x, y, 0f), float.MaxValue, positions);
    }

    private static LaneDecision Decision(FordsMatch match, int lane = 0, MatchSide side = MatchSide.West) =>
        match.LastDecisions.Single(d => d.Side == side && d.Lane == lane);

    private static LaneOrder Order(FordsMatch match, int lane = 0) =>
        match.GetView(WestSlot).Lanes.Single(l => l.Index == lane).Order;

    [TestMethod]
    public void TrySetLaneOrder_SeizeWithNothingOpen_IsRefusedAndTheOrderStays()
    {
        var match = Match();

        Assert.AreEqual(CommandResult.NothingToSeize, match.TrySetLaneOrder(WestSlot, 0, LaneOrder.Seize));
        Assert.AreEqual(LaneOrder.Push, Order(match));
        Assert.AreEqual(0f, match.GetView(WestSlot).Lanes[0].CooldownLeft, "a refused order costs no cooldown");
    }

    [TestMethod]
    public void TrySetLaneOrder_SeizeWithAnOpenPoint_SetsTheOrderAndStartsTheCooldown()
    {
        var match = Match(points: new[] { Point() });

        Assert.AreEqual(CommandResult.Ok, match.TrySetLaneOrder(WestSlot, 0, LaneOrder.Seize));
        Assert.AreEqual(LaneOrder.Seize, Order(match));
        Assert.IsTrue(match.GetView(WestSlot).Lanes[0].CooldownLeft > 0f);
    }

    [TestMethod]
    public void GetView_CanSeize_FollowsTheOpenObjectives()
    {
        Assert.IsFalse(Match().GetView(WestSlot).Lanes[0].CanSeize);
        Assert.IsTrue(Match(points: new[] { Point() }).GetView(WestSlot).Lanes[0].CanSeize);
    }

    [TestMethod]
    public void GetView_ALaneAwayFromTheObjective_CannotSeizeIt()
    {
        // Between lanes 0 and 1, so lane 2 has two lanes between itself and the objective's neighbours: lane bars stop it walking there.
        var match = Match(points: new[] { Point() });

        var lanes = match.GetView(WestSlot).Lanes;

        Assert.IsTrue(lanes.Single(l => l.Index == 0).CanSeize);
        Assert.IsTrue(lanes.Single(l => l.Index == 1).CanSeize);
        Assert.IsFalse(lanes.Single(l => l.Index == 2).CanSeize);
        Assert.AreEqual(CommandResult.NothingToSeize, match.TrySetLaneOrder(WestSlot, 2, LaneOrder.Seize));
    }

    [TestMethod]
    public void Tick_Seize_SendsTheLaneToTheNearestOpenObjective()
    {
        var match = Match(new[] { Camp() }, new[] { Point() });
        match.Tick(Snap(1f, new[] { Lane(MatchSide.West, 0, 200f) }));
        match.TrySetLaneOrder(WestSlot, 0, LaneOrder.Seize);

        match.Tick(Snap(1f, new[] { Lane(MatchSide.West, 0, 200f) }));

        var decision = Decision(match);
        Assert.AreEqual(LaneBranch.Seize, decision.Branch);
        Assert.AreEqual(LaneCommand.HoldAt(CampAt), decision.Command);
        StringAssert.Contains(decision.ToLogBody(), "seize=camp_0");
    }

    [TestMethod]
    public void Tick_SeizeStaysOnItsObjective_WhenAnotherBecomesNearer()
    {
        var match = Match(new[] { Camp() }, new[] { Point() });
        match.Tick(Snap(1f, new[] { Lane(MatchSide.West, 0, 200f) }));
        match.TrySetLaneOrder(WestSlot, 0, LaneOrder.Seize);

        match.Tick(Snap(1f, new[] { Lane(MatchSide.West, 0, 650f) }));

        Assert.AreEqual(LaneCommand.HoldAt(CampAt), Decision(match).Command);
    }

    [TestMethod]
    public void Tick_SeizeAtACampWithAUnitInAlertRange_Charges()
    {
        var match = Match(new[] { Camp() });
        match.TrySetLaneOrder(WestSlot, 0, LaneOrder.Seize);

        match.Tick(Snap(1f, new[] { Lane(MatchSide.West, 0, CampAt.X, y: CampAt.Y - CampRules.AlertRange + 1f) }));

        Assert.AreEqual(LaneBranch.Seize, Decision(match).Branch);
        Assert.AreEqual(LaneCommand.Charge, Decision(match).Command);
    }

    [TestMethod]
    public void Tick_SeizeAtACapturePoint_HoldsThereAndReturnsToPushOnceHeld()
    {
        var match = Match(points: new[] { Point() });
        match.Tick(Snap(1f, new[] { Lane(MatchSide.West, 0, PointAt.X, y: PointAt.Y) }));
        match.TrySetLaneOrder(WestSlot, 0, LaneOrder.Seize);
        var lane = Lane(MatchSide.West, 0, PointAt.X, y: PointAt.Y);
        var onPoint = new[] { new CaptureCounts("point_0", 10, 0) };

        match.Tick(Snap(1f, new[] { lane }, capture: onPoint));

        Assert.AreEqual(LaneOrder.Seize, Order(match));
        Assert.AreEqual(LaneCommand.HoldAt(PointAt), Decision(match).Command);

        for (var i = 0; i < 40 && Order(match) == LaneOrder.Seize; i++)
            match.Tick(Snap(1f, new[] { lane }, capture: onPoint));

        Assert.AreEqual(MatchSide.West, match.GetView(WestSlot).CapturePoints[0].Owner);
        Assert.AreEqual(LaneOrder.Push, Order(match), "the side holds the point: the lane pushes on");
        Assert.AreNotEqual(LaneBranch.Seize, Decision(match).Branch);
    }

    [TestMethod]
    public void Tick_ACampClearedByAnyone_ReturnsTheSeizingLaneToPush()
    {
        var match = Match(new[] { Camp(count: 1) });
        match.TrySetLaneOrder(WestSlot, 0, LaneOrder.Seize);

        match.OnCampUnitKilled("camp_0", null);
        match.Tick(Snap(1f, new[] { Lane(MatchSide.West, 0, 100f) }));

        Assert.AreEqual(LaneOrder.Push, Order(match));
    }

    [TestMethod]
    public void Tick_ReturnToPush_KeepsTheCooldownOfTheSeizeOrder()
    {
        var match = Match(new[] { Camp(count: 1) });
        match.TrySetLaneOrder(WestSlot, 0, LaneOrder.Seize);
        var cooldown = match.GetView(WestSlot).Lanes[0].CooldownLeft;

        match.OnCampUnitKilled("camp_0", null);
        match.Tick(Snap(1f, new[] { Lane(MatchSide.West, 0, 100f) }));

        var left = match.GetView(WestSlot).Lanes[0].CooldownLeft;
        Assert.IsTrue(left > 0f && left <= cooldown, $"cooldown {left} of {cooldown}");
    }

    [TestMethod]
    public void Tick_SeizeWithAnEnemyOfAnotherLaneClose_Charges()
    {
        var match = Match(new[] { Camp() });
        match.TrySetLaneOrder(WestSlot, 0, LaneOrder.Seize);
        var west = Lane(MatchSide.West, 0, 200f);
        var eastOtherLane = Lane(MatchSide.East, 1, 205f, y: 10f);

        match.Tick(Snap(1f, new[] { west, eastOtherLane }));

        Assert.AreEqual(LaneBranch.Charge, Decision(match).Branch);
    }

    [TestMethod]
    public void TrySetLaneOrder_AnotherOrderAfterSeize_DropsTheObjective()
    {
        var match = Match(new[] { Camp() });
        match.TrySetLaneOrder(WestSlot, 0, LaneOrder.Seize);
        match.Tick(Snap(FordsMatchConfig.Default.Lanes.OrderCooldownSeconds + 1f, new[] { Lane(MatchSide.West, 0, 100f) }));

        Assert.AreEqual(CommandResult.Ok, match.TrySetLaneOrder(WestSlot, 0, LaneOrder.Push));
        match.Tick(Snap(1f, new[] { Lane(MatchSide.West, 0, 100f) }));

        Assert.AreEqual(LaneOrder.Push, Order(match));
        Assert.AreEqual(LaneBranch.March, Decision(match).Branch);
        Assert.IsFalse(Decision(match).ToLogBody().Contains("seize="));
    }

    [TestMethod]
    public void GetView_ThePointASideHolds_IsOpenOnlyToTheOtherSide()
    {
        var match = Match(points: new[] { Point() });
        for (var i = 0; i < 40; i++)
            match.Tick(Snap(1f, capture: new[] { new CaptureCounts("point_0", 10, 0) }));

        Assert.IsFalse(match.GetView(WestSlot).Lanes[0].CanSeize);
        Assert.IsTrue(match.GetView(EastSlot).Lanes[0].CanSeize);
    }

    [TestMethod]
    public void GetView_AClearedCamp_IsOpenToNobody()
    {
        var match = Match(new[] { Camp(count: 1) });

        match.OnCampUnitKilled("camp_0", null);

        Assert.IsFalse(match.GetView(WestSlot).Lanes[0].CanSeize);
        Assert.IsFalse(match.GetView(EastSlot).Lanes[0].CanSeize);
    }

    // Camps inside the match.

    [TestMethod]
    public void OnCampUnitKilled_TheLastUnit_PaysTheKillersSideAndRaisesTheEvent()
    {
        var match = Match(new[] { Camp(count: 2) });
        match.RegisterUnit(1, Unit(10, MatchSide.West, WestSlot));
        var gold = match.GetView(WestSlot).Gold;

        Assert.IsNull(match.OnCampUnitKilled("camp_0", Key(10)));
        Assert.AreEqual(gold, match.GetView(WestSlot).Gold, "no pay before the last unit");

        var cleared = match.OnCampUnitKilled("camp_0", Key(10));

        Assert.AreEqual(MatchSide.West, cleared!.Side);
        Assert.AreEqual(gold + 250, match.GetView(WestSlot).Gold);
        var effects = match.Tick(Snap(1f));
        var e = effects.Events.Single(x => x.Kind == MatchEventKind.CampCleared);
        Assert.AreEqual(MatchSide.West, e.Side);
        Assert.AreEqual("camp_0", e.Subject);
        Assert.AreEqual(250, e.Amount);
    }

    [TestMethod]
    public void OnCampUnitKilled_AnUnknownKiller_ClearsTheCampAndPaysNobody()
    {
        var match = Match(new[] { Camp(count: 1) });
        var gold = match.GetView(WestSlot).Gold;

        var cleared = match.OnCampUnitKilled("camp_0", Key(77));

        Assert.IsNull(cleared!.Side);
        Assert.AreEqual(gold, match.GetView(WestSlot).Gold);
        Assert.IsFalse(match.Tick(Snap(1f)).Events.Any(x => x.Kind == MatchEventKind.CampCleared));
    }

    [TestMethod]
    public void Tick_AUnitComesCloseThenLeaves_OrdersTheCampOutAndHome()
    {
        var match = Match(new[] { Camp() });
        var near = Lane(MatchSide.East, 0, CampAt.X, y: CampAt.Y - CampRules.AlertRange + 1f);
        var far = Lane(MatchSide.East, 0, CampAt.X + 200f);

        Assert.AreEqual(0, match.Tick(Snap(1f, new[] { far })).CampOrders.Count);

        var alerted = match.Tick(Snap(1f, new[] { near })).CampOrders.Single();
        Assert.AreEqual(new CampOrder("camp_0", true), alerted);

        Assert.AreEqual(0, match.Tick(Snap(1f, new[] { near })).CampOrders.Count, "the order is given once");

        var home = match.Tick(Snap(1f, new[] { far })).CampOrders.Single();
        Assert.AreEqual(new CampOrder("camp_0", false), home);
    }
}
