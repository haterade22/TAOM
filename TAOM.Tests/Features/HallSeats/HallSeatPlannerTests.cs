using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.HallSeats.Domain;

namespace TAOM.Tests.Features.HallSeats;

/// <summary>The seat rule of Hall Seats (docs/features/hall-seats.md): how many lords sit, and which.</summary>
[TestClass]
public class HallSeatPlannerTests
{
    private static HallSeatCandidate Lord(string id, bool mustKeep = false, bool ownerClan = false, bool clanLeader = false,
        int tier = 1, float renown = 0f) =>
        new HallSeatCandidate(new object(), id, mustKeep, ownerClan, clanLeader, tier, renown);

    private static string[] SeatedIds(IReadOnlyList<HallSeatCandidate> lords, HallSeatPlan plan) =>
        lords.Where(l => !plan.IsHeldBack(l.Token)).Select(l => l.HeroId).OrderBy(x => x).ToArray();

    [TestMethod]
    public void EnoughSpots_SeatsEveryLord()
    {
        var lords = new[] { Lord("a"), Lord("b"), Lord("c") };

        var plan = HallSeatPlanner.Plan(lords, freeLordSpots: 5, spotsTakenByOthers: 0);

        Assert.AreEqual(0, plan.HeldBack.Count);
        Assert.AreEqual(3, plan.Seated);
        Assert.AreEqual(5, plan.Seats);
    }

    [TestMethod]
    public void FortyLordsFiveSpots_SeatsFive_HoldsBackThirtyFive()
    {
        var lords = Enumerable.Range(0, 40).Select(i => Lord("lord_" + i.ToString("D2"))).ToList();

        var plan = HallSeatPlanner.Plan(lords, freeLordSpots: 5, spotsTakenByOthers: 0);

        Assert.AreEqual(5, plan.Seated);
        Assert.AreEqual(35, plan.HeldBack.Count);
    }

    [TestMethod]
    public void OtherCharactersOnLordSpots_ReduceTheSeats()
    {
        var lords = Enumerable.Range(0, 6).Select(i => Lord("l" + i)).ToList();

        var plan = HallSeatPlanner.Plan(lords, freeLordSpots: 5, spotsTakenByOthers: 2);

        Assert.AreEqual(3, plan.Seats);
        Assert.AreEqual(3, plan.Seated);
    }

    [TestMethod]
    public void MoreTakenThanFree_ZeroSeats_NeverNegative()
    {
        var lords = new[] { Lord("a"), Lord("b") };

        var plan = HallSeatPlanner.Plan(lords, freeLordSpots: 1, spotsTakenByOthers: 4);

        Assert.AreEqual(0, plan.Seats);
        Assert.AreEqual(2, plan.HeldBack.Count);
    }

    [TestMethod]
    public void MustKeepLords_AlwaysSit_EvenPastTheSeatCount()
    {
        var lords = new[] { Lord("owner", mustKeep: true), Lord("quest", mustKeep: true), Lord("other", ownerClan: true, clanLeader: true) };

        var plan = HallSeatPlanner.Plan(lords, freeLordSpots: 1, spotsTakenByOthers: 0);

        CollectionAssert.AreEqual(new[] { "owner", "quest" }, SeatedIds(lords, plan));
        Assert.AreEqual(1, plan.HeldBack.Count);
    }

    [TestMethod]
    public void MustKeepLords_UseSeatsFirst_TheRestFillWhatIsLeft()
    {
        var lords = new[] { Lord("keep", mustKeep: true), Lord("x", tier: 6), Lord("y", tier: 1) };

        var plan = HallSeatPlanner.Plan(lords, freeLordSpots: 2, spotsTakenByOthers: 0);

        CollectionAssert.AreEqual(new[] { "keep", "x" }, SeatedIds(lords, plan));
    }

    [TestMethod]
    public void Ranking_OwnerClanThenClanLeaderThenTierThenRenown()
    {
        var lords = new[]
        {
            Lord("renown_high", tier: 3, renown: 900f),
            Lord("renown_low", tier: 3, renown: 100f),
            Lord("tier_high", tier: 5),
            Lord("leader", clanLeader: true, tier: 1),
            Lord("owner_clan", ownerClan: true, tier: 0),
        };

        string[] Seat(int n) => SeatedIds(lords, HallSeatPlanner.Plan(lords, n, 0));

        CollectionAssert.AreEqual(new[] { "owner_clan" }, Seat(1));
        CollectionAssert.AreEqual(new[] { "leader", "owner_clan" }, Seat(2));
        CollectionAssert.AreEqual(new[] { "leader", "owner_clan", "tier_high" }, Seat(3));
        CollectionAssert.AreEqual(new[] { "leader", "owner_clan", "renown_high", "tier_high" }, Seat(4));
    }

    [TestMethod]
    public void EqualRank_BreaksTiesByHeroId_SoTheSameVisitSeatsTheSameLords()
    {
        var lords = new[] { Lord("c"), Lord("a"), Lord("b") };

        var first = SeatedIds(lords, HallSeatPlanner.Plan(lords, 2, 0));
        var again = SeatedIds(lords.Reverse().ToArray(), HallSeatPlanner.Plan(lords.Reverse().ToArray(), 2, 0));

        CollectionAssert.AreEqual(new[] { "a", "b" }, first);
        CollectionAssert.AreEqual(first, again);
    }

    [TestMethod]
    public void HeldBack_IsByReference_NotByHeroId()
    {
        var twinA = Lord("same");
        var twinB = Lord("same");

        var plan = HallSeatPlanner.Plan(new[] { twinA, twinB }, 1, 0);

        Assert.AreEqual(1, plan.HeldBack.Count);
        Assert.AreNotEqual(plan.IsHeldBack(twinA.Token), plan.IsHeldBack(twinB.Token));
        Assert.IsFalse(plan.IsHeldBack(new object()));
    }

    [TestMethod]
    public void NoLords_NothingHeldBack()
    {
        var plan = HallSeatPlanner.Plan(new List<HallSeatCandidate>(), 0, 3);

        Assert.AreEqual(0, plan.HeldBack.Count);
        Assert.AreEqual(0, plan.Seated);
    }
}
