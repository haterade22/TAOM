using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;
using static TAOM.Tests.Features.FordsOfWar.Domain.FordsMatchFixture;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>
/// The engine layer could not spawn a request (<see cref="FordsMatch.OnSpawnFailed"/>): a hero gives back what its request
/// charged, frees its slot and leaves the offer lists; a bought pack gives back its unspawned share and leaves the offer
/// lists; a free wave changes nothing. Both sides have 5000 gold; the AI is on its slow profile and never acts.
/// </summary>
[TestClass]
public class FordsMatchSpawnFailureTests
{
    private const int Gold = 5000;

    private static FordsMatch Match() =>
        new(Config(c => c with { Economy = c.Economy with { StartingGold = Gold } }), Layout(), Factions(), Slots("slow"), new SeededFordsRandom(1));

    private static int GoldOf(FordsMatch match) => match.GetView(WestSlot).Gold;

    private static SpawnRequest BuyHeroAndTick(FordsMatch match, string heroId)
    {
        Assert.AreEqual(CommandResult.Ok, match.TryBuyHero(WestSlot, 0, heroId));
        return match.Tick(Snap(1f)).Spawns.Single(s => s.IsHero && s.TroopId == heroId);
    }

    private static void RegisterHero(FordsMatch match, SpawnRequest request, int key)
    {
        match.RegisterUnit(request.RequestId, Unit(key, MatchSide.West, WestSlot, role: UnitRole.Hero, heroId: request.TroopId));
    }

    private static SpawnRequest BuyPackAndTick(FordsMatch match, UnitOffer offer)
    {
        Assert.AreEqual(CommandResult.Ok, match.TryBuyPack(WestSlot, 0, offer.OfferId));
        return match.Tick(Snap(1f)).Spawns.Single(s => !s.IsHero && s.OwnerSlot == WestSlot);
    }

    // --- heroes ------------------------------------------------------------------------------------------------------

    [TestMethod]
    public void OnSpawnFailed_BoughtHero_RefundsThePurchasePrice()
    {
        var match = Match();
        var request = BuyHeroAndTick(match, HeroA.HeroId);
        Assert.AreEqual(Gold - HeroA.Price, GoldOf(match));

        match.OnSpawnFailed(request.RequestId, 1);

        Assert.AreEqual(Gold, GoldOf(match));
    }

    [TestMethod]
    public void OnSpawnFailed_BoughtHero_FreesTheAliveSlotAndRemovesTheHeroEntry()
    {
        var match = Match();
        var request = BuyHeroAndTick(match, HeroA.HeroId);
        Assert.AreEqual(1, match.GetView(WestSlot).HeroSlotsFree);

        match.OnSpawnFailed(request.RequestId, 1);

        var view = match.GetView(WestSlot);
        Assert.AreEqual(2, view.HeroSlotsFree);
        Assert.IsFalse(view.Heroes.Any(h => h.HeroId == HeroA.HeroId));
    }

    [TestMethod]
    public void OnSpawnFailed_BoughtHero_TakesTheOfferOutOfTheViewAndRefusesTheBuy()
    {
        var match = Match();
        var request = BuyHeroAndTick(match, HeroA.HeroId);

        match.OnSpawnFailed(request.RequestId, 1);

        Assert.IsFalse(match.GetView(WestSlot).HeroOffers.Any(o => o.HeroId == HeroA.HeroId));
        Assert.IsTrue(match.GetView(WestSlot).HeroOffers.Any(o => o.HeroId == HeroB.HeroId));
        Assert.AreEqual(CommandResult.UnknownOffer, match.TryBuyHero(WestSlot, 0, HeroA.HeroId));
        Assert.AreEqual(CommandResult.HeroNotDead, match.TryBuyback(WestSlot, HeroA.HeroId));
        Assert.AreEqual(Gold, GoldOf(match));
    }

    [TestMethod]
    public void OnSpawnFailed_BoughtHero_LeavesTheOtherHeroAndTheOtherSideAlone()
    {
        var match = Match();
        var request = BuyHeroAndTick(match, HeroA.HeroId);

        match.OnSpawnFailed(request.RequestId, 1);

        Assert.AreEqual(CommandResult.Ok, match.TryBuyHero(WestSlot, 0, HeroB.HeroId));
        Assert.IsTrue(match.GetView(EastSlot).HeroOffers.Any(o => o.HeroId == HeroA.HeroId));
        Assert.AreEqual(Gold, GoldOf(match) + HeroB.Price);
    }

    [TestMethod]
    public void OnSpawnFailed_FailedBuyback_RefundsTheBuybackPriceAndRemovesTheHero()
    {
        var match = Match();
        var first = BuyHeroAndTick(match, HeroA.HeroId);
        RegisterHero(match, first, 10);
        match.OnUnitKilled(Key(10), null);
        var beforeBuyback = GoldOf(match);
        Assert.AreEqual(CommandResult.Ok, match.TryBuyback(WestSlot, HeroA.HeroId));
        Assert.IsTrue(GoldOf(match) < beforeBuyback);
        var respawn = match.Tick(Snap(0.5f)).Spawns.Single(s => s.IsHero);

        match.OnSpawnFailed(respawn.RequestId, 1);

        Assert.AreEqual(beforeBuyback, GoldOf(match));
        var view = match.GetView(WestSlot);
        Assert.IsFalse(view.Heroes.Any(h => h.HeroId == HeroA.HeroId));
        Assert.IsFalse(view.HeroOffers.Any(o => o.HeroId == HeroA.HeroId));
        Assert.AreEqual(2, view.HeroSlotsFree);
    }

    [TestMethod]
    public void OnSpawnFailed_FailedNaturalRespawn_RefundsNothingAndFreesTheSlot()
    {
        var match = Match();
        var first = BuyHeroAndTick(match, HeroA.HeroId);
        RegisterHero(match, first, 10);
        match.OnUnitKilled(Key(10), null);
        var gold = GoldOf(match);
        var respawn = match.Tick(Snap(70f)).Spawns.Single(s => s.IsHero);

        match.OnSpawnFailed(respawn.RequestId, 1);

        Assert.AreEqual(gold, GoldOf(match));
        var view = match.GetView(WestSlot);
        Assert.IsFalse(view.Heroes.Any(h => h.HeroId == HeroA.HeroId));
        Assert.IsFalse(view.HeroOffers.Any(o => o.HeroId == HeroA.HeroId));
        Assert.AreEqual(2, view.HeroSlotsFree);
    }

    [TestMethod]
    public void OnSpawnFailed_HeroThatSpawnedFine_IsNotAffectedByALaterCall()
    {
        var match = Match();
        var request = BuyHeroAndTick(match, HeroA.HeroId);
        RegisterHero(match, request, 10);

        match.OnSpawnFailed(request.RequestId, 1);

        Assert.AreEqual(Gold - HeroA.Price, GoldOf(match));
        Assert.IsTrue(match.GetView(WestSlot).Heroes.Any(h => h.HeroId == HeroA.HeroId && h.State == HeroState.Alive));
    }

    // --- packs -------------------------------------------------------------------------------------------------------

    [TestMethod]
    public void OnSpawnFailed_PackWithThreeOfFiveUnspawned_RefundsThreeFifthsOfThePrice()
    {
        var match = Match();
        var request = BuyPackAndTick(match, Elite);
        Assert.AreEqual(5, request.Count);
        match.RegisterUnit(request.RequestId, Unit(10, MatchSide.West, WestSlot));
        match.RegisterUnit(request.RequestId, Unit(11, MatchSide.West, WestSlot));
        Assert.AreEqual(Gold - Elite.PackPrice, GoldOf(match));

        match.OnSpawnFailed(request.RequestId, 3);

        Assert.AreEqual(Gold - Elite.PackPrice + Elite.PackPrice * 3 / 5, GoldOf(match));
    }

    [TestMethod]
    public void OnSpawnFailed_PackWithThreeOfFiveUnspawned_TakesTheOfferAwayFromThatSideOnly()
    {
        var match = Match();
        var request = BuyPackAndTick(match, Elite);

        match.OnSpawnFailed(request.RequestId, 3);

        Assert.IsFalse(match.GetView(WestSlot).UnitOffers.Any(o => o.OfferId == Elite.OfferId));
        Assert.IsTrue(match.GetView(WestSlot).UnitOffers.Any(o => o.OfferId == Footmen.OfferId));
        Assert.IsTrue(match.GetView(EastSlot).UnitOffers.Any(o => o.OfferId == Elite.OfferId));
        Assert.AreEqual(CommandResult.UnknownOffer, match.TryBuyPack(WestSlot, 0, Elite.OfferId));
        Assert.AreEqual(CommandResult.Ok, match.TryBuyPack(WestSlot, 0, Footmen.OfferId));
    }

    [TestMethod]
    public void OnSpawnFailed_PackRefund_RoundsDown()
    {
        var odd = new UnitOffer("odd", "odd_troop", UnitRole.Infantry, 3, 100);
        var factions = Factions().ToDictionary(p => p.Key, p => p.Value with { Units = p.Value.Units.Append(odd).ToList() });
        var config = Config(c => c with { Economy = c.Economy with { StartingGold = Gold } });
        var match = new FordsMatch(config, Layout(), factions, Slots("slow"), new SeededFordsRandom(1));
        var request = BuyPackAndTick(match, odd);

        match.OnSpawnFailed(request.RequestId, 1);

        Assert.AreEqual(Gold - 100 + 33, GoldOf(match));
    }

    [TestMethod]
    public void OnSpawnFailed_CalledTwiceForOneRequest_RefundsOnce()
    {
        var match = Match();
        var request = BuyPackAndTick(match, Elite);

        match.OnSpawnFailed(request.RequestId, 3);
        var gold = GoldOf(match);
        match.OnSpawnFailed(request.RequestId, 3);

        Assert.AreEqual(gold, GoldOf(match));
    }

    [TestMethod]
    public void OnSpawnFailed_MoreUnspawnedThanTheRequestHolds_RefundsAtMostWhatIsUnspawned()
    {
        var match = Match();
        var request = BuyPackAndTick(match, Elite);
        match.RegisterUnit(request.RequestId, Unit(10, MatchSide.West, WestSlot));

        match.OnSpawnFailed(request.RequestId, 9);

        Assert.AreEqual(Gold - Elite.PackPrice + Elite.PackPrice * 4 / 5, GoldOf(match));
    }

    [TestMethod]
    public void OnSpawnFailed_PackThatFullySpawned_RefundsNothing()
    {
        var match = Match();
        var request = BuyPackAndTick(match, Elite);
        for (var i = 0; i < request.Count; i++)
            match.RegisterUnit(request.RequestId, Unit(10 + i, MatchSide.West, WestSlot));

        match.OnSpawnFailed(request.RequestId, 2);

        Assert.AreEqual(Gold - Elite.PackPrice, GoldOf(match));
        Assert.IsTrue(match.GetView(WestSlot).UnitOffers.Any(o => o.OfferId == Elite.OfferId));
    }

    [TestMethod]
    public void OnSpawnFailed_PackSplitAcrossTwoRequests_RefundsEachShareAndKeepsTheOfferGone()
    {
        var config = Config(c => c with
        {
            Economy = c.Economy with { StartingGold = Gold },
            Waves = c.Waves with { MaxSpawnsPerTick = 2 },
        });
        var match = new FordsMatch(config, Layout(), Factions(), Slots("slow"), new SeededFordsRandom(1));
        Assert.AreEqual(CommandResult.Ok, match.TryBuyPack(WestSlot, 0, Elite.OfferId));
        var first = match.Tick(Snap(1f)).Spawns.Single();
        var second = match.Tick(Snap(1f)).Spawns.Single();

        match.OnSpawnFailed(first.RequestId, first.Count);
        match.OnSpawnFailed(second.RequestId, second.Count);

        Assert.AreEqual(2, first.Count);
        Assert.AreEqual(Gold - Elite.PackPrice + Elite.PackPrice * 2 / 5 + Elite.PackPrice * 2 / 5, GoldOf(match));
        Assert.IsFalse(match.GetView(WestSlot).UnitOffers.Any(o => o.OfferId == Elite.OfferId));
    }

    // --- free waves and unknown requests -----------------------------------------------------------------------------

    [TestMethod]
    public void OnSpawnFailed_FreeWave_ChangesNothing()
    {
        var config = WithFreeWaves(Config(c => c with { Economy = c.Economy with { StartingGold = Gold } }));
        var match = new FordsMatch(config, Layout(), Factions(), Slots("slow"), new SeededFordsRandom(1));
        var wave = match.Tick(Snap(1f)).Spawns.First(s => s.Side == MatchSide.West && s.TroopId == Footmen.TroopId);
        var before = match.GetView(WestSlot);

        match.OnSpawnFailed(wave.RequestId, wave.Count);

        var after = match.GetView(WestSlot);
        Assert.AreEqual(before.Gold, after.Gold);
        CollectionAssert.AreEqual(before.UnitOffers.Select(o => o.OfferId).ToList(), after.UnitOffers.Select(o => o.OfferId).ToList());
        CollectionAssert.AreEqual(before.HeroOffers.Select(o => o.HeroId).ToList(), after.HeroOffers.Select(o => o.HeroId).ToList());
        Assert.AreEqual(CommandResult.Ok, match.TryBuyPack(WestSlot, 0, Footmen.OfferId));
    }

    [TestMethod]
    public void OnSpawnFailed_UnknownRequestId_ChangesNothing()
    {
        var match = Match();

        match.OnSpawnFailed(9999, 4);

        Assert.AreEqual(Gold, GoldOf(match));
        Assert.AreEqual(Factions()[MatchSide.West].Units.Count, match.GetView(WestSlot).UnitOffers.Count);
    }

    // --- the AI ------------------------------------------------------------------------------------------------------

    [TestMethod]
    public void Tick_AfterTheAisHeroFailedToSpawn_TheAiNeverBuysThatHeroAgain()
    {
        var config = Config(c => c with { Economy = c.Economy with { StartingGold = Gold } });
        var match = new FordsMatch(config, Layout(), Factions(), Slots("hard"), new SeededFordsRandom(1));
        var failed = (SpawnRequest?)null;
        var bought = new List<string>();

        for (var i = 0; i < 40; i++)
        {
            var effects = match.Tick(Snap(5f));
            foreach (var spawn in effects.Spawns.Where(s => s.IsHero && s.Side == MatchSide.East))
            {
                if (failed == null)
                {
                    failed = spawn;
                    match.OnSpawnFailed(spawn.RequestId, 1);
                }
                else
                {
                    bought.Add(spawn.TroopId);
                }
            }
        }

        Assert.IsNotNull(failed);
        Assert.IsFalse(bought.Contains(failed!.TroopId));
        Assert.IsFalse(match.GetView(EastSlot).HeroOffers.Any(o => o.HeroId == failed.TroopId));
    }
}
