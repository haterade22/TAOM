using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Domain;
using static TAOM.Tests.Features.FordsOfWar.Domain.FordsMatchFixture;

namespace TAOM.Tests.Features.FordsOfWar;

[TestClass]
public class FordsSessionTests
{
    private static readonly Vec3f CameraStart = new(40f, 25f, 60f);
    private static readonly Vec3f CameraLookAt = new(960f, 25f, 0f);

    private static FordsSession Filled(out IFordsBattlefieldAdapter battlefield, out FordsMatch match)
    {
        battlefield = Substitute.For<IFordsBattlefieldAdapter>();
        match = Create();
        var polygon = new List<Vec3f> { new(0f, 0f, 0f), new(1000f, 0f, 0f), new(1000f, 50f, 0f) };
        var session = new FordsSession();
        session.Fill(new object(), match, WestSlot, battlefield, CameraStart, CameraLookAt, polygon);
        return session;
    }

    [TestMethod]
    public void BeforeFill_IsInactiveAndEveryCommandIsRefused()
    {
        var session = new FordsSession();

        Assert.IsFalse(session.IsActive);
        Assert.IsNull(session.GetView());
        Assert.AreEqual(CommandResult.NotLive, session.TryBuyPack(0, "inf"));
        Assert.AreEqual(CommandResult.NotLive, session.TryBuyHero(0, "hero_a"));
        Assert.AreEqual(CommandResult.NotLive, session.TryBuyback("hero_a"));
        Assert.AreEqual(CommandResult.NotLive, session.TrySetLaneOrder(0, LaneOrder.Hold));
        Assert.AreEqual(0, session.DrainEvents().Count);
        Assert.AreEqual(0, session.BoundaryPolygon.Count);
    }

    [TestMethod]
    public void DisplayName_BeforeFill_ReturnsTheId()
    {
        Assert.AreEqual("lord_1_17", new FordsSession().DisplayName("lord_1_17"));
    }

    [TestMethod]
    public void Fill_MakesTheSessionActiveWithTheCameraNumbers()
    {
        var session = Filled(out _, out _);

        Assert.IsTrue(session.IsActive);
        Assert.AreEqual(CameraStart, session.CameraStart);
        Assert.AreEqual(CameraLookAt, session.CameraLookAt);
        Assert.AreEqual(3, session.BoundaryPolygon.Count);
    }

    [TestMethod]
    public void GetView_AfterFill_IsTheHumanSlotsView()
    {
        var session = Filled(out _, out _);

        var view = session.GetView();

        Assert.IsNotNull(view);
        Assert.AreEqual(WestSlot, view!.Slot);
        Assert.AreEqual(MatchSide.West, view.Side);
        Assert.AreEqual(1000, view.Gold);
    }

    [TestMethod]
    public void TryBuyPack_GoesToTheMatchForTheHumanSlotAndSpendsGold()
    {
        var session = Filled(out _, out _);

        var result = session.TryBuyPack(0, "inf");

        Assert.AreEqual(CommandResult.Ok, result);
        Assert.AreEqual(900, session.GetView()!.Gold);
    }

    [TestMethod]
    public void TryBuyPack_UnknownOffer_PassesTheRefusalThrough()
    {
        var session = Filled(out _, out _);

        Assert.AreEqual(CommandResult.UnknownOffer, session.TryBuyPack(0, "nope"));
    }

    [TestMethod]
    public void TryBuyHero_FieldsTheHeroForTheHumanSlot()
    {
        var session = Filled(out _, out _);

        Assert.AreEqual(CommandResult.Ok, session.TryBuyHero(1, HeroB.HeroId));
        Assert.AreEqual(600, session.GetView()!.Gold);
    }

    [TestMethod]
    public void TryBuyback_HeroNotDead_IsRefused()
    {
        var session = Filled(out _, out _);

        Assert.AreEqual(CommandResult.HeroNotDead, session.TryBuyback(HeroA.HeroId));
    }

    [TestMethod]
    public void TrySetLaneOrder_SetsTheOrderThatTheViewReports()
    {
        var session = Filled(out _, out _);

        Assert.AreEqual(CommandResult.Ok, session.TrySetLaneOrder(0, LaneOrder.Hold));
        Assert.AreEqual(LaneOrder.Hold, session.GetView()!.Lanes.Single(l => l.Index == 0).Order);
    }

    [TestMethod]
    public void DisplayName_AskedTwice_ReadsTheAdapterOnce()
    {
        var session = Filled(out var battlefield, out _);
        battlefield.CharacterName("lord_1_17").Returns("Sauron");

        var first = session.DisplayName("lord_1_17");
        var second = session.DisplayName("lord_1_17");

        Assert.AreEqual("Sauron", first);
        Assert.AreEqual("Sauron", second);
        battlefield.Received(1).CharacterName("lord_1_17");
    }

    [TestMethod]
    public void DrainEvents_ReturnsTheEventsInOrderThenNothing()
    {
        var session = Filled(out _, out _);
        session.PushEvents(new[]
        {
            new MatchEvent(MatchEventKind.PackBought, MatchSide.West, "inf", 100),
            new MatchEvent(MatchEventKind.TowerDestroyed, MatchSide.East, "tower_east_0_0", 300),
        });

        var drained = session.DrainEvents();

        CollectionAssert.AreEqual(new[] { MatchEventKind.PackBought, MatchEventKind.TowerDestroyed }, drained.Select(e => e.Kind).ToArray());
        Assert.AreEqual(0, session.DrainEvents().Count);
    }

    [TestMethod]
    public void PushEvents_MoreThanTheFeedHolds_KeepsTheNewest()
    {
        var session = Filled(out _, out _);
        session.PushEvents(Enumerable.Range(0, 400).Select(i => new MatchEvent(MatchEventKind.PackBought, MatchSide.West, "e" + i, i)));

        var drained = session.DrainEvents();

        Assert.AreEqual(256, drained.Count);
        Assert.AreEqual("e399", drained.Last().Subject);
        Assert.AreEqual("e144", drained.First().Subject);
    }

    [TestMethod]
    public void Clear_EndsTheSessionAndDropsTheFeedAndTheNameCache()
    {
        var session = Filled(out var battlefield, out _);
        battlefield.CharacterName("lord_1_17").Returns("Sauron");
        session.DisplayName("lord_1_17");
        session.PushEvents(new[] { new MatchEvent(MatchEventKind.PackBought, MatchSide.West, "inf", 100) });

        session.Clear();

        Assert.IsFalse(session.IsActive);
        Assert.IsNull(session.GetView());
        Assert.AreEqual(0, session.DrainEvents().Count);
        Assert.AreEqual(CommandResult.NotLive, session.TryBuyPack(0, "inf"));
        Assert.AreEqual("lord_1_17", session.DisplayName("lord_1_17"));
    }

    [TestMethod]
    public void PushEvents_WhileInactive_IsIgnored()
    {
        var session = new FordsSession();

        session.PushEvents(new[] { new MatchEvent(MatchEventKind.PackBought, MatchSide.West, "inf", 100) });

        Assert.AreEqual(0, session.DrainEvents().Count);
    }
}
