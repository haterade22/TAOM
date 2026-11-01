using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;
using static TAOM.Tests.Features.FordsOfWar.Domain.FordsMatchFixture;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>
/// An ordinary request the scheduler issued but the engine layer has not spawned yet still holds its lane's room, so a
/// director that spreads a request over several ticks cannot push a lane past its live cap.
/// </summary>
[TestClass]
public class FordsMatchLaneReserveTests
{
    private const int LaneCap = 90;

    private static FordsMatch Match() =>
        Create(Config(c => c with { Waves = c.Waves with { MaxLivePerLanePerSide = LaneCap, MaxLivePerSide = 300, MaxSpawnsPerTick = 6 } }),
            Slots("slow"));

    private static MatchSnapshot LaneSnap(int westLane0) =>
        Snap(1f, new[] { Live(MatchSide.West, 0, westLane0), Live(MatchSide.West, 1, 0) });

    private static int OrdinaryInLane0(MatchEffects effects) =>
        effects.Spawns.Where(s => !s.IsHero && s.Side == MatchSide.West && s.Lane == 0).Sum(s => s.Count);

    [TestMethod]
    public void Tick_RequestPartlySpawned_ReservesTheUnspawnedUnitsAgainstTheLane()
    {
        var match = Match();
        Assert.AreEqual(CommandResult.Ok, match.TryBuyPack(WestSlot, 0, Footmen.OfferId));
        var first = match.Tick(LaneSnap(LaneCap - 6));
        var issued = first.Spawns.Single(s => !s.IsHero);
        Assert.AreEqual(6, issued.Count);
        for (var i = 0; i < 5; i++)
            match.RegisterUnit(issued.RequestId, Unit(100 + i, MatchSide.West, WestSlot));

        // 84 + 5 spawned = 89 live; the sixth unit of the first request is still waiting in the director.
        var second = match.Tick(LaneSnap(LaneCap - 1));

        Assert.AreEqual(0, OrdinaryInLane0(second));
    }

    [TestMethod]
    public void Tick_RequestFullySpawned_ReleasesItsRoom()
    {
        var match = Match();
        match.TryBuyPack(WestSlot, 0, Footmen.OfferId);
        var issued = match.Tick(LaneSnap(LaneCap - 6)).Spawns.Single(s => !s.IsHero);
        for (var i = 0; i < issued.Count; i++)
            match.RegisterUnit(issued.RequestId, Unit(100 + i, MatchSide.West, WestSlot));

        var second = match.Tick(LaneSnap(LaneCap - 3));

        Assert.AreEqual(3, OrdinaryInLane0(second));
    }

    [TestMethod]
    public void Tick_RequestFailedToSpawn_ReleasesItsRoom()
    {
        var match = Match();
        match.TryBuyPack(WestSlot, 0, Footmen.OfferId);
        var issued = match.Tick(LaneSnap(LaneCap - 6)).Spawns.Single(s => !s.IsHero);
        match.RegisterUnit(issued.RequestId, Unit(100, MatchSide.West, WestSlot));
        match.OnSpawnFailed(issued.RequestId, 5);

        var second = match.Tick(LaneSnap(LaneCap - 5));

        Assert.AreEqual(4, OrdinaryInLane0(second));
    }

    [TestMethod]
    public void Tick_PendingHero_DoesNotReserveTheLane()
    {
        var match = Match();
        match.TryBuyHero(WestSlot, 0, HeroA.HeroId);
        var first = match.Tick(LaneSnap(LaneCap));
        Assert.IsTrue(first.Spawns.Single().IsHero);
        match.TryBuyPack(WestSlot, 0, Footmen.OfferId);

        var second = match.Tick(LaneSnap(LaneCap - 4));

        Assert.AreEqual(4, OrdinaryInLane0(second));
    }

    [TestMethod]
    public void Tick_SideCapBinds_AnUnspawnedRequestInOneLaneLeavesLessRoomForTheOtherLane()
    {
        var match = Create(Config(c => c with { Waves = c.Waves with { MaxLivePerLanePerSide = LaneCap, MaxLivePerSide = 10, MaxSpawnsPerTick = 6 } }),
            Slots("slow"));
        Assert.AreEqual(CommandResult.Ok, match.TryBuyPack(WestSlot, 0, Footmen.OfferId));
        var first = match.Tick(LaneSnap(0));
        Assert.AreEqual(6, first.Spawns.Single(s => !s.IsHero).Count, "none of lane 0's six units has spawned yet");
        Assert.AreEqual(CommandResult.Ok, match.TryBuyPack(WestSlot, 1, Footmen.OfferId));

        var second = match.Tick(LaneSnap(0));

        var lane1 = second.Spawns.Where(s => !s.IsHero && s.Side == MatchSide.West && s.Lane == 1).Sum(s => s.Count);
        Assert.AreEqual(4, lane1, "the side holds 10: six are reserved for lane 0, so lane 1 gets the other four");
    }
}
