using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>Free-wave timers and tiers, the purchased pack queue, and the caps that split a request across ticks.</summary>
[TestClass]
public class WaveSchedulerTests
{
    private const float Interval = 40f;

    private static readonly UnitOffer Spears = new("spears", "spearman", UnitRole.Spear, 20, 150);
    private static readonly UnitOffer Archers = new("archers", "archer", UnitRole.Ranged, 10, 120);

    private static FordsWaveConfig Config(int perLane = 90, int perSide = 300, int perTick = 1000, int queued = 3, float interval = Interval) =>
        new(interval, perLane, perSide, perTick, queued);

    private static IReadOnlyList<FreeWaveTier> Tiers() => new List<FreeWaveTier>
    {
        new(0f, new List<UnitCount> { new("spearman", 5), new("archer", 3) }),
        new(10f, new List<UnitCount> { new("spearman", 8) }),
        new(20f, new List<UnitCount> { new("elite", 4) }),
    };

    private static IReadOnlyDictionary<MatchSide, FactionCatalog> Factions(IReadOnlyList<FreeWaveTier>? tiers = null)
    {
        var waves = tiers ?? new List<FreeWaveTier>();
        var units = new List<UnitOffer> { Spears, Archers };
        return new Dictionary<MatchSide, FactionCatalog>
        {
            [MatchSide.West] = new("west", "gondor", units, new List<HeroOffer>(), waves),
            [MatchSide.East] = new("east", "mordor", units, new List<HeroOffer>(), waves),
        };
    }

    private static int Owner(MatchSide side, int lane) => (side == MatchSide.West ? 10 : 20) + lane;

    private static WaveScheduler Scheduler(FordsWaveConfig? config = null, IReadOnlyList<FreeWaveTier>? tiers = null, int lanes = 2) =>
        new(config ?? Config(), Factions(tiers), lanes, Owner);

    private static int Live(MatchSide side, int lane) => 0;

    private static int LiveSide(MatchSide side) => 0;

    private static IReadOnlyList<SpawnRequest> Tick(WaveScheduler s, float dt, float minute = 0f) =>
        s.Tick(dt, minute, Live, LiveSide);

    [TestMethod]
    public void NextRequestId_EachCall_IncreasesByOne()
    {
        var s = Scheduler();

        var first = s.NextRequestId();
        var second = s.NextRequestId();
        var third = s.NextRequestId();

        Assert.IsTrue(first < second && second < third);
        Assert.AreEqual(first + 1, second);
        Assert.AreEqual(second + 1, third);
    }

    [TestMethod]
    public void Tick_FirstTick_EmitsOneFreeWaveForEveryLaneAndSide()
    {
        var s = Scheduler(tiers: Tiers());

        var requests = Tick(s, 0.1f);

        Assert.AreEqual(8, requests.Count);
        foreach (var side in new[] { MatchSide.West, MatchSide.East })
        {
            for (var lane = 0; lane < 2; lane++)
            {
                var mine = requests.Where(r => r.Side == side && r.Lane == lane).ToList();
                Assert.AreEqual(8, mine.Sum(r => r.Count));
                Assert.IsTrue(mine.All(r => !r.IsHero));
            }
        }
    }

    [TestMethod]
    public void Tick_FreeWave_BelongsToTheSlotThatOwnsTheLane()
    {
        var s = Scheduler(tiers: Tiers());

        var requests = Tick(s, 0.1f);

        Assert.IsTrue(requests.All(r => r.OwnerSlot == Owner(r.Side, r.Lane)));
    }

    [TestMethod]
    public void Tick_FreeWave_TakesTheRoleOfTheMatchingOffer()
    {
        var s = Scheduler(tiers: Tiers());

        var requests = Tick(s, 0.1f).Where(r => r.Side == MatchSide.West && r.Lane == 0).ToList();

        Assert.AreEqual(UnitRole.Spear, requests.Single(r => r.TroopId == "spearman").Role);
        Assert.AreEqual(UnitRole.Ranged, requests.Single(r => r.TroopId == "archer").Role);
    }

    [TestMethod]
    public void Tick_FreeWaveTroopWithoutAnOffer_FallsBackToInfantry()
    {
        var s = Scheduler(tiers: Tiers());

        var requests = Tick(s, 0.1f, 25f);

        Assert.IsTrue(requests.All(r => r.TroopId == "elite" && r.Role == UnitRole.Infantry));
    }

    [TestMethod]
    public void Tick_BeforeTheIntervalElapses_EmitsNoSecondWave()
    {
        var s = Scheduler(tiers: Tiers());
        Tick(s, 0.1f);

        var requests = Tick(s, 30f);

        Assert.AreEqual(0, requests.Count);
    }

    [TestMethod]
    public void Tick_AfterTheInterval_EmitsTheNextWave()
    {
        var s = Scheduler(tiers: Tiers());
        Tick(s, 0.1f);
        Tick(s, 30f);

        var requests = Tick(s, 15f);

        Assert.AreEqual(8, requests.Count);
    }

    [TestMethod]
    public void Tick_ALongStall_EmitsOnlyOneWavePerStream()
    {
        var s = Scheduler(tiers: Tiers());
        Tick(s, 0.1f);

        var requests = Tick(s, 400f);

        Assert.AreEqual(8, requests.Count);
    }

    [TestMethod]
    public void Tick_NonPositiveInterval_DisablesFreeWaves()
    {
        var s = Scheduler(Config(interval: 0f), Tiers());

        Assert.AreEqual(0, Tick(s, 100f).Count);
    }

    [DataTestMethod]
    [DataRow(0f, "spearman", 5)]
    [DataRow(9.99f, "spearman", 5)]
    [DataRow(10f, "spearman", 8)]
    [DataRow(19f, "spearman", 8)]
    [DataRow(20f, "elite", 4)]
    [DataRow(90f, "elite", 4)]
    public void Tick_FreeWave_UsesTheHighestTierNotAboveTheMinute(float minute, string troop, int count)
    {
        var s = Scheduler(tiers: Tiers(), lanes: 1);

        var west = Tick(s, 0.1f, minute).Where(r => r.Side == MatchSide.West).ToList();

        Assert.AreEqual(count, west.Single(r => r.TroopId == troop).Count);
    }

    [TestMethod]
    public void Tick_NoTierStartedYet_EmitsNothing()
    {
        var late = new List<FreeWaveTier> { new(5f, new List<UnitCount> { new("spearman", 5) }) };
        var s = Scheduler(tiers: late, lanes: 1);

        Assert.AreEqual(0, Tick(s, 0.1f, 1f).Count);
        Assert.AreEqual(5, Tick(s, 41f, 6f).Single(r => r.Side == MatchSide.West).Count);
    }

    [TestMethod]
    public void Tick_UnsortedTiers_StillPicksTheHighestStartedOne()
    {
        var shuffled = Tiers().Reverse().ToList();
        var s = Scheduler(tiers: shuffled, lanes: 1);

        var west = Tick(s, 0.1f, 12f).Where(r => r.Side == MatchSide.West).ToList();

        Assert.AreEqual(8, west.Single().Count);
    }

    [TestMethod]
    public void Tick_FreeWave_ThatCannotStartBecauseOfTheCaps_DoesNotPileUp()
    {
        var s = Scheduler(Config(perLane: 20), Tiers(), lanes: 1);
        for (var i = 0; i < 5; i++)
            s.Tick(41f, 0f, (side, lane) => 20, side => 20);

        var requests = s.Tick(0.1f, 0f, Live, LiveSide);

        Assert.AreEqual(8, requests.Where(r => r.Side == MatchSide.West).Sum(r => r.Count));
    }

    [TestMethod]
    public void TryQueuePack_FreeSlot_ReturnsOkAndCountsThePack()
    {
        var s = Scheduler();

        var result = s.TryQueuePack(MatchSide.West, 1, 11, Spears);

        Assert.AreEqual(CommandResult.Ok, result);
        Assert.AreEqual(1, s.QueuedPacks(MatchSide.West, 1));
        Assert.AreEqual(0, s.QueuedPacks(MatchSide.West, 0));
        Assert.AreEqual(0, s.QueuedPacks(MatchSide.East, 1));
    }

    [TestMethod]
    public void TryQueuePack_AtTheQueueLimit_ReturnsQueueFull()
    {
        var s = Scheduler(Config(queued: 3));
        for (var i = 0; i < 3; i++)
            Assert.AreEqual(CommandResult.Ok, s.TryQueuePack(MatchSide.West, 0, 10, Spears));

        var result = s.TryQueuePack(MatchSide.West, 0, 10, Spears);

        Assert.AreEqual(CommandResult.QueueFull, result);
        Assert.AreEqual(3, s.QueuedPacks(MatchSide.West, 0));
    }

    [TestMethod]
    public void TryQueuePack_LimitIsPerLaneAndSide()
    {
        var s = Scheduler(Config(queued: 1));
        s.TryQueuePack(MatchSide.West, 0, 10, Spears);

        Assert.AreEqual(CommandResult.Ok, s.TryQueuePack(MatchSide.West, 1, 11, Spears));
        Assert.AreEqual(CommandResult.Ok, s.TryQueuePack(MatchSide.East, 0, 20, Spears));
    }

    [DataTestMethod]
    [DataRow(-1)]
    [DataRow(2)]
    public void TryQueuePack_LaneOutOfRange_ReturnsUnknownLane(int lane)
    {
        var s = Scheduler(lanes: 2);

        Assert.AreEqual(CommandResult.UnknownLane, s.TryQueuePack(MatchSide.West, lane, 10, Spears));
    }

    [TestMethod]
    public void TryQueuePack_EmptyPack_ReturnsUnknownOffer()
    {
        var s = Scheduler();

        var result = s.TryQueuePack(MatchSide.West, 0, 10, Spears with { PackSize = 0 });

        Assert.AreEqual(CommandResult.UnknownOffer, result);
    }

    [TestMethod]
    public void Tick_QueuedPack_EmitsOneRequestForTheBuyer()
    {
        var s = Scheduler();
        s.TryQueuePack(MatchSide.East, 1, 77, Archers);

        var requests = Tick(s, 0.1f);

        var request = requests.Single();
        Assert.AreEqual(MatchSide.East, request.Side);
        Assert.AreEqual(1, request.Lane);
        Assert.AreEqual("archer", request.TroopId);
        Assert.AreEqual(10, request.Count);
        Assert.AreEqual(77, request.OwnerSlot);
        Assert.AreEqual(UnitRole.Ranged, request.Role);
        Assert.IsFalse(request.IsHero);
    }

    [TestMethod]
    public void Tick_EmittedPack_FreesItsQueueSlot()
    {
        var s = Scheduler(Config(queued: 1));
        s.TryQueuePack(MatchSide.West, 0, 10, Spears);
        Tick(s, 0.1f);

        Assert.AreEqual(0, s.QueuedPacks(MatchSide.West, 0));
        Assert.AreEqual(CommandResult.Ok, s.TryQueuePack(MatchSide.West, 0, 10, Spears));
    }

    [TestMethod]
    public void Tick_PacksInOneLane_EmitInPurchaseOrder()
    {
        var s = Scheduler();
        s.TryQueuePack(MatchSide.West, 0, 10, Archers);
        s.TryQueuePack(MatchSide.West, 0, 10, Spears);

        var requests = Tick(s, 0.1f);

        CollectionAssert.AreEqual(new[] { "archer", "spearman" }, requests.Select(r => r.TroopId).ToArray());
    }

    [TestMethod]
    public void Tick_PurchasedPack_ComesBeforeTheFreeWaveOfItsLane()
    {
        var s = Scheduler(tiers: Tiers(), lanes: 1);
        s.TryQueuePack(MatchSide.West, 0, 10, Archers);

        var west = Tick(s, 0.1f).Where(r => r.Side == MatchSide.West).ToList();

        Assert.AreEqual(10, west[0].Count);
        Assert.AreEqual(10, west[0].OwnerSlot);
    }

    [TestMethod]
    public void Tick_RequestIds_IncreaseAcrossTicks()
    {
        var s = Scheduler(tiers: Tiers());
        s.TryQueuePack(MatchSide.West, 0, 10, Spears);

        var ids = Tick(s, 0.1f).Select(r => r.RequestId).ToList();
        ids.AddRange(Tick(s, 41f).Select(r => r.RequestId));

        Assert.IsTrue(ids.Count > 10);
        for (var i = 1; i < ids.Count; i++)
            Assert.IsTrue(ids[i] > ids[i - 1], $"id {ids[i]} after {ids[i - 1]}");
    }

    [TestMethod]
    public void Tick_RequestIds_ShareTheCounterWithNextRequestId()
    {
        var s = Scheduler();
        s.TryQueuePack(MatchSide.West, 0, 10, Spears);
        var before = s.NextRequestId();

        var request = Tick(s, 0.1f).Single();
        var after = s.NextRequestId();

        Assert.IsTrue(request.RequestId > before);
        Assert.IsTrue(after > request.RequestId);
    }

    [TestMethod]
    public void Tick_LargePack_IsSplitAcrossTicksAndNeverDropped()
    {
        var s = Scheduler(Config(perTick: 6));
        s.TryQueuePack(MatchSide.West, 0, 10, Spears);

        var counts = new List<int>();
        for (var i = 0; i < 6; i++)
            counts.Add(Tick(s, 0.1f).Sum(r => r.Count));

        CollectionAssert.AreEqual(new[] { 6, 6, 6, 2, 0, 0 }, counts);
    }

    [TestMethod]
    public void Tick_SplitPack_KeepsItsQueueSlotUntilTheLastUnitIsOut()
    {
        var s = Scheduler(Config(perTick: 6, queued: 1));
        s.TryQueuePack(MatchSide.West, 0, 10, Spears);
        Tick(s, 0.1f);

        Assert.AreEqual(1, s.QueuedPacks(MatchSide.West, 0));
        Assert.AreEqual(CommandResult.QueueFull, s.TryQueuePack(MatchSide.West, 0, 10, Spears));
    }

    [TestMethod]
    public void Tick_SplitPack_KeepsTheBuyerAndTroopOnEveryPart()
    {
        var s = Scheduler(Config(perTick: 6));
        s.TryQueuePack(MatchSide.West, 0, 42, Spears);

        var parts = new List<SpawnRequest>();
        for (var i = 0; i < 4; i++)
            parts.AddRange(Tick(s, 0.1f));

        Assert.AreEqual(20, parts.Sum(r => r.Count));
        Assert.IsTrue(parts.All(r => r.OwnerSlot == 42 && r.TroopId == "spearman" && r.Role == UnitRole.Spear));
    }

    [TestMethod]
    public void Tick_TickBudget_IsSharedByEveryLaneAndSide()
    {
        var s = Scheduler(Config(perTick: 6), tiers: Tiers());

        var total = Tick(s, 0.1f).Sum(r => r.Count);

        Assert.AreEqual(6, total);
    }

    [TestMethod]
    public void Tick_TightBudget_RotatesWhichStreamGoesFirst()
    {
        var s = Scheduler(Config(perTick: 1));
        s.TryQueuePack(MatchSide.West, 0, 10, Spears);
        s.TryQueuePack(MatchSide.East, 0, 20, Spears);

        var firsts = new List<MatchSide>();
        for (var i = 0; i < 4; i++)
            firsts.Add(Tick(s, 0.1f).Single().Side);

        Assert.IsTrue(firsts.Count(f => f == MatchSide.West) >= 1);
        Assert.IsTrue(firsts.Count(f => f == MatchSide.East) >= 1);
        Assert.AreEqual(2, firsts.Take(2).Distinct().Count());
    }

    [TestMethod]
    public void Tick_LaneAtItsLiveCap_EmitsNothingForThatLane()
    {
        var s = Scheduler(Config(perLane: 90));
        s.TryQueuePack(MatchSide.West, 0, 10, Spears);

        var requests = s.Tick(0.1f, 0f, (side, lane) => 90, side => 90);

        Assert.AreEqual(0, requests.Count);
        Assert.AreEqual(1, s.QueuedPacks(MatchSide.West, 0));
    }

    [TestMethod]
    public void Tick_LaneNearItsLiveCap_EmitsOnlyTheRoomLeft()
    {
        var s = Scheduler(Config(perLane: 90));
        s.TryQueuePack(MatchSide.West, 0, 10, Spears);

        var requests = s.Tick(0.1f, 0f, (side, lane) => 85, side => 85);

        Assert.AreEqual(5, requests.Single().Count);
        Assert.AreEqual(1, s.QueuedPacks(MatchSide.West, 0));
    }

    [TestMethod]
    public void Tick_LiveCapOfOneLane_DoesNotBlockTheOtherLane()
    {
        var s = Scheduler(Config(perLane: 90));
        s.TryQueuePack(MatchSide.West, 0, 10, Spears);
        s.TryQueuePack(MatchSide.West, 1, 11, Spears);

        var requests = s.Tick(0.1f, 0f, (side, lane) => lane == 0 ? 90 : 0, side => 90);

        Assert.AreEqual(1, requests.Single().Lane);
    }

    [TestMethod]
    public void Tick_SideAtItsLiveCap_EmitsNothingForThatSide()
    {
        var s = Scheduler(Config(perSide: 300));
        s.TryQueuePack(MatchSide.West, 0, 10, Spears);
        s.TryQueuePack(MatchSide.East, 0, 20, Spears);

        var requests = s.Tick(0.1f, 0f, Live, side => side == MatchSide.West ? 300 : 0);

        Assert.AreEqual(MatchSide.East, requests.Single().Side);
    }

    [TestMethod]
    public void Tick_SideCap_IsSharedByTheLanesOfThatSide()
    {
        var s = Scheduler(Config(perSide: 300));
        s.TryQueuePack(MatchSide.West, 0, 10, Spears);
        s.TryQueuePack(MatchSide.West, 1, 11, Spears);

        var requests = s.Tick(0.1f, 0f, Live, side => 290);

        Assert.AreEqual(10, requests.Sum(r => r.Count));
    }

    [TestMethod]
    public void Tick_UnitsEmittedThisTick_CountTowardTheLaneCap()
    {
        var s = Scheduler(Config(perLane: 90));
        s.TryQueuePack(MatchSide.West, 0, 10, Spears);
        s.TryQueuePack(MatchSide.West, 0, 10, Archers);

        var requests = s.Tick(0.1f, 0f, (side, lane) => 70, side => 70);

        Assert.AreEqual(20, requests.Sum(r => r.Count));
        Assert.AreEqual(1, s.QueuedPacks(MatchSide.West, 0));
    }

    [TestMethod]
    public void Tick_CapLifts_ThePackedUnitsFollowOnALaterTick()
    {
        var s = Scheduler(Config(perLane: 90));
        s.TryQueuePack(MatchSide.West, 0, 10, Spears);
        var emitted = s.Tick(0.1f, 0f, (side, lane) => 85, side => 85).Sum(r => r.Count);

        emitted += Tick(s, 0.1f).Sum(r => r.Count);

        Assert.AreEqual(20, emitted);
        Assert.AreEqual(0, s.QueuedPacks(MatchSide.West, 0));
    }

    [TestMethod]
    public void Tick_NothingQueuedAndNoFreeWave_ReturnsEmpty()
    {
        var s = Scheduler();

        Assert.AreEqual(0, Tick(s, 5f).Count);
    }

    [TestMethod]
    public void TakePackOffer_PackRequest_ReturnsTheBoughtOfferOnceForEachSplitPart()
    {
        var s = Scheduler(Config(perTick: 8));
        s.TryQueuePack(MatchSide.West, 0, 10, Spears);
        var first = s.Tick(0.1f, 0f, Live, LiveSide).Single();
        var second = s.Tick(0.1f, 0f, Live, LiveSide).Single();

        Assert.AreEqual(Spears, s.TakePackOffer(first.RequestId));
        Assert.AreEqual(Spears, s.TakePackOffer(second.RequestId));
        Assert.IsNull(s.TakePackOffer(first.RequestId));
    }

    [TestMethod]
    public void TakePackOffer_FreeWaveRequestOrUnknownId_ReturnsNull()
    {
        var s = Scheduler(tiers: Tiers());
        var wave = Tick(s, 0.1f);

        Assert.IsTrue(wave.Count > 0);
        Assert.IsTrue(wave.All(r => s.TakePackOffer(r.RequestId) == null));
        Assert.IsNull(s.TakePackOffer(9999));
    }
}
