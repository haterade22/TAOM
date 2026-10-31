using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>Hero limit, death and respawn timing, buyback pricing, and the respawn spawn requests.</summary>
[TestClass]
public class HeroRosterTests
{
    private static readonly FordsHeroConfig Config = FordsMatchConfig.Default.Heroes;

    private static int _nextId;

    private static int NextId() => ++_nextId;

    private static HeroRoster Roster() => new(Config);

    [TestMethod]
    public void TryField_FreeSlot_FieldsAnAliveHero()
    {
        var roster = Roster();

        var result = roster.TryField(MatchSide.West, "sauron", 1, 2, 800);

        Assert.AreEqual(CommandResult.Ok, result);
        var hero = roster.Get(MatchSide.West, "sauron");
        Assert.IsNotNull(hero);
        Assert.AreEqual(HeroState.Alive, hero!.State);
        Assert.AreEqual(1, hero.OwnerSlot);
        Assert.AreEqual(2, hero.Lane);
        Assert.AreEqual(800, hero.Price);
        Assert.AreEqual(1, roster.AliveCount(MatchSide.West));
    }

    [TestMethod]
    public void TryField_AtTheLimit_IsRefused()
    {
        var roster = Roster();
        roster.TryField(MatchSide.West, "a", 1, 0, 100);
        roster.TryField(MatchSide.West, "b", 1, 0, 100);

        var result = roster.TryField(MatchSide.West, "c", 1, 0, 100);

        Assert.AreEqual(CommandResult.HeroLimit, result);
        Assert.IsNull(roster.Get(MatchSide.West, "c"));
        Assert.AreEqual(Config.MaxAlivePerSide, roster.AliveCount(MatchSide.West));
    }

    [TestMethod]
    public void TryField_LimitIsPerSide()
    {
        var roster = Roster();
        roster.TryField(MatchSide.West, "a", 1, 0, 100);
        roster.TryField(MatchSide.West, "b", 1, 0, 100);

        Assert.AreEqual(CommandResult.Ok, roster.TryField(MatchSide.East, "a", 2, 0, 100));
    }

    [TestMethod]
    public void TryField_SameHeroAgain_IsAlreadyFielded()
    {
        var roster = Roster();
        roster.TryField(MatchSide.West, "sauron", 1, 0, 800);

        Assert.AreEqual(CommandResult.HeroAlreadyFielded, roster.TryField(MatchSide.West, "sauron", 1, 1, 800));
    }

    [TestMethod]
    public void TryField_ADeadHero_IsAlreadyFielded()
    {
        var roster = Roster();
        roster.TryField(MatchSide.West, "sauron", 1, 0, 800);
        roster.OnDied(MatchSide.West, "sauron", 100f, 3f);

        Assert.AreEqual(CommandResult.HeroAlreadyFielded, roster.TryField(MatchSide.West, "sauron", 1, 0, 800));
    }

    [TestMethod]
    public void TryField_AlreadyFieldedAtTheLimit_ReportsAlreadyFielded()
    {
        var roster = Roster();
        roster.TryField(MatchSide.West, "a", 1, 0, 100);
        roster.TryField(MatchSide.West, "b", 1, 0, 100);

        Assert.AreEqual(CommandResult.HeroAlreadyFielded, roster.TryField(MatchSide.West, "a", 1, 0, 100));
    }

    [TestMethod]
    public void TryField_ADeadHeroFreesItsLimitSlot()
    {
        var roster = Roster();
        roster.TryField(MatchSide.West, "a", 1, 0, 100);
        roster.TryField(MatchSide.West, "b", 1, 0, 100);
        roster.OnDied(MatchSide.West, "a", 10f, 0f);

        Assert.AreEqual(CommandResult.Ok, roster.TryField(MatchSide.West, "c", 1, 0, 100));
    }

    [TestMethod]
    public void RespawnDelay_GrowsWithTheMinute()
    {
        var roster = Roster();

        Assert.AreEqual(60f, roster.RespawnDelay(0f), 1e-3f);
        Assert.AreEqual(120f, roster.RespawnDelay(10f), 1e-3f);
    }

    [TestMethod]
    public void RespawnDelay_NegativeMinute_UsesTheBase()
    {
        Assert.AreEqual(60f, Roster().RespawnDelay(-5f), 1e-3f);
    }

    [TestMethod]
    public void BuybackPrice_ScalesWithPriceAndMinute()
    {
        var roster = Roster();

        Assert.AreEqual(500, roster.BuybackPrice(1000, 0f));
        Assert.AreEqual(1000, roster.BuybackPrice(1000, 10f));
        Assert.AreEqual(1500, roster.BuybackPrice(1000, 20f));
    }

    [TestMethod]
    public void BuybackPrice_RoundsToNearest()
    {
        Assert.AreEqual(2, Roster().BuybackPrice(3, 0f));
        Assert.AreEqual(0, Roster().BuybackPrice(0, 5f));
    }

    [TestMethod]
    public void BuybackPrice_ForAFieldedHero_UsesItsPrice()
    {
        var roster = Roster();
        roster.TryField(MatchSide.West, "sauron", 1, 0, 800);

        Assert.AreEqual(400, roster.BuybackPrice(MatchSide.West, "sauron", 0f));
        Assert.AreEqual(0, roster.BuybackPrice(MatchSide.West, "nobody", 0f));
    }

    [TestMethod]
    public void OnDied_AliveHero_BecomesDeadWithARespawnTime()
    {
        var roster = Roster();
        roster.TryField(MatchSide.West, "sauron", 1, 0, 800);

        var changed = roster.OnDied(MatchSide.West, "sauron", 300f, 5f);

        Assert.IsTrue(changed);
        var hero = roster.Get(MatchSide.West, "sauron")!;
        Assert.AreEqual(HeroState.Dead, hero.State);
        Assert.AreEqual(300f + 60f + 30f, hero.RespawnAt, 1e-3f);
        Assert.AreEqual(0, roster.AliveCount(MatchSide.West));
    }

    [TestMethod]
    public void OnDied_UnknownOrAlreadyDead_ReturnsFalse()
    {
        var roster = Roster();
        roster.TryField(MatchSide.West, "sauron", 1, 0, 800);
        roster.OnDied(MatchSide.West, "sauron", 300f, 5f);

        Assert.IsFalse(roster.OnDied(MatchSide.West, "sauron", 400f, 6f));
        Assert.IsFalse(roster.OnDied(MatchSide.West, "nobody", 400f, 6f));
        Assert.AreEqual(390f, roster.Get(MatchSide.West, "sauron")!.RespawnAt, 1e-3f);
    }

    [TestMethod]
    public void DueRespawns_BeforeTheTimer_ReturnsNothing()
    {
        var roster = Roster();
        roster.TryField(MatchSide.West, "sauron", 1, 0, 800);
        roster.OnDied(MatchSide.West, "sauron", 0f, 0f);

        Assert.AreEqual(0, roster.DueRespawns(59f, NextId).Count);
        Assert.AreEqual(HeroState.Dead, roster.Get(MatchSide.West, "sauron")!.State);
    }

    [TestMethod]
    public void DueRespawns_WhenTheTimerEnds_EmitsAHeroSpawnRequestAndRevives()
    {
        var roster = Roster();
        roster.TryField(MatchSide.East, "witch_king", 4, 2, 900);
        roster.OnDied(MatchSide.East, "witch_king", 0f, 0f);

        var requests = roster.DueRespawns(60f, () => 77);

        var r = requests.Single();
        Assert.AreEqual(77, r.RequestId);
        Assert.AreEqual(MatchSide.East, r.Side);
        Assert.AreEqual(2, r.Lane);
        Assert.AreEqual("witch_king", r.TroopId);
        Assert.AreEqual(1, r.Count);
        Assert.AreEqual(4, r.OwnerSlot);
        Assert.AreEqual(UnitRole.Hero, r.Role);
        Assert.IsTrue(r.IsHero);
        Assert.AreEqual(HeroState.Alive, roster.Get(MatchSide.East, "witch_king")!.State);
        Assert.AreEqual(0, roster.DueRespawns(61f, NextId).Count);
    }

    [TestMethod]
    public void DueRespawns_SeveralDue_RespawnEarliestFirstAndNumberRequestsInOrder()
    {
        var roster = new HeroRoster(Config with { MaxAlivePerSide = 3 });
        roster.TryField(MatchSide.West, "a", 1, 0, 100);
        roster.TryField(MatchSide.West, "b", 1, 0, 100);
        roster.OnDied(MatchSide.West, "b", 10f, 0f);
        roster.OnDied(MatchSide.West, "a", 50f, 0f);
        var id = 0;

        var requests = roster.DueRespawns(500f, () => ++id);

        Assert.AreEqual("b", requests[0].TroopId);
        Assert.AreEqual(1, requests[0].RequestId);
        Assert.AreEqual("a", requests[1].TroopId);
        Assert.AreEqual(2, requests[1].RequestId);
    }

    [TestMethod]
    public void DueRespawns_SideIsAtTheLimit_WaitsForAFreeSlot()
    {
        var roster = Roster();
        roster.TryField(MatchSide.West, "a", 1, 0, 100);
        roster.TryField(MatchSide.West, "b", 1, 0, 100);
        roster.OnDied(MatchSide.West, "a", 0f, 0f);
        roster.TryField(MatchSide.West, "c", 1, 0, 100);

        Assert.AreEqual(0, roster.DueRespawns(500f, NextId).Count);
        Assert.AreEqual(HeroState.Dead, roster.Get(MatchSide.West, "a")!.State);

        roster.OnDied(MatchSide.West, "c", 500f, 0f);
        var requests = roster.DueRespawns(500f, NextId);

        Assert.AreEqual("a", requests.Single().TroopId);
    }

    [TestMethod]
    public void TryBuyback_DeadHero_MakesTheRespawnDueNow()
    {
        var roster = Roster();
        roster.TryField(MatchSide.West, "sauron", 1, 0, 800);
        roster.OnDied(MatchSide.West, "sauron", 0f, 0f);

        var result = roster.TryBuyback(MatchSide.West, "sauron", 12f);

        Assert.AreEqual(CommandResult.Ok, result);
        Assert.AreEqual(12f, roster.Get(MatchSide.West, "sauron")!.RespawnAt, 1e-3f);
        Assert.AreEqual(1, roster.DueRespawns(12f, NextId).Count);
    }

    [TestMethod]
    public void TryBuyback_AliveOrUnknownHero_IsNotDead()
    {
        var roster = Roster();
        roster.TryField(MatchSide.West, "sauron", 1, 0, 800);

        Assert.AreEqual(CommandResult.HeroNotDead, roster.TryBuyback(MatchSide.West, "sauron", 12f));
        Assert.AreEqual(CommandResult.HeroNotDead, roster.TryBuyback(MatchSide.West, "nobody", 12f));
    }

    [TestMethod]
    public void TryBuyback_DeadHero_HoldsItsSlotFromThePurchase()
    {
        var roster = Roster();
        roster.TryField(MatchSide.West, "a", 1, 0, 100);
        roster.TryField(MatchSide.West, "b", 1, 0, 100);
        roster.OnDied(MatchSide.West, "b", 0f, 0f);

        var bought = roster.TryBuyback(MatchSide.West, "b", 12f);

        Assert.AreEqual(CommandResult.Ok, bought);
        Assert.AreEqual(2, roster.AliveCount(MatchSide.West));
        Assert.AreEqual(CommandResult.HeroLimit, roster.TryField(MatchSide.West, "c", 1, 0, 100));
    }

    [TestMethod]
    public void DueRespawns_BoughtBackHeroWithTheSideFull_StillSpawnsAndKeepsTheCount()
    {
        var roster = Roster();
        roster.TryField(MatchSide.West, "a", 1, 0, 100);
        roster.TryField(MatchSide.West, "b", 1, 0, 100);
        roster.OnDied(MatchSide.West, "b", 0f, 0f);
        roster.TryBuyback(MatchSide.West, "b", 12f);

        var requests = roster.DueRespawns(12f, NextId);

        Assert.AreEqual("b", requests.Single().TroopId);
        Assert.AreEqual(HeroState.Alive, roster.Get(MatchSide.West, "b")!.State);
        Assert.AreEqual(2, roster.AliveCount(MatchSide.West));
        Assert.AreEqual(0, roster.DueRespawns(13f, NextId).Count);
    }

    [TestMethod]
    public void TryBuyback_AlreadyBoughtBack_IsRefused()
    {
        var roster = Roster();
        roster.TryField(MatchSide.West, "b", 1, 0, 100);
        roster.OnDied(MatchSide.West, "b", 0f, 0f);
        roster.TryBuyback(MatchSide.West, "b", 12f);

        Assert.AreEqual(CommandResult.HeroNotDead, roster.TryBuyback(MatchSide.West, "b", 13f));
    }

    [TestMethod]
    public void TryBuyback_RespawnTimerAlreadyEnded_IsRefused()
    {
        var roster = Roster();
        roster.TryField(MatchSide.West, "b", 1, 0, 100);
        roster.OnDied(MatchSide.West, "b", 0f, 0f);

        Assert.AreEqual(CommandResult.HeroNotDead, roster.TryBuyback(MatchSide.West, "b", 60f));
        Assert.AreEqual(CommandResult.HeroNotDead, roster.TryBuyback(MatchSide.West, "b", 90f));
    }

    [TestMethod]
    public void ToSpawnRequest_FreshHero_DescribesTheSpawn()
    {
        var roster = Roster();
        roster.TryField(MatchSide.West, "sauron", 3, 1, 800);

        var request = roster.Get(MatchSide.West, "sauron")!.ToSpawnRequest(5);

        Assert.AreEqual(new SpawnRequest(5, MatchSide.West, 1, "sauron", 1, 3, UnitRole.Hero, true), request);
    }

    [TestMethod]
    public void Remove_FieldedHero_DropsTheEntryAndItsSlot()
    {
        var roster = Roster();
        roster.TryField(MatchSide.West, "a", 1, 0, 100);
        roster.TryField(MatchSide.West, "b", 1, 0, 100);

        var removed = roster.Remove(MatchSide.West, "a");

        Assert.IsTrue(removed);
        Assert.IsNull(roster.Get(MatchSide.West, "a"));
        Assert.AreEqual(1, roster.AliveCount(MatchSide.West));
        Assert.AreEqual(CommandResult.Ok, roster.TryField(MatchSide.West, "c", 1, 0, 100));
    }

    [TestMethod]
    public void Remove_DeadHeroWithAPaidBuyback_FreesTheReservedSlot()
    {
        var roster = Roster();
        roster.TryField(MatchSide.West, "a", 1, 0, 100);
        roster.OnDied(MatchSide.West, "a", 10f, 0f);
        roster.TryBuyback(MatchSide.West, "a", 20f);
        Assert.AreEqual(1, roster.AliveCount(MatchSide.West));

        roster.Remove(MatchSide.West, "a");

        Assert.AreEqual(0, roster.AliveCount(MatchSide.West));
    }

    [TestMethod]
    public void Remove_UnknownHeroOrOtherSide_ReturnsFalseAndChangesNothing()
    {
        var roster = Roster();
        roster.TryField(MatchSide.West, "a", 1, 0, 100);

        Assert.IsFalse(roster.Remove(MatchSide.West, "nobody"));
        Assert.IsFalse(roster.Remove(MatchSide.East, "a"));
        Assert.IsNotNull(roster.Get(MatchSide.West, "a"));
    }

    [TestMethod]
    public void Heroes_ListsEverySideInFieldingOrder()
    {
        var roster = Roster();
        roster.TryField(MatchSide.East, "x", 2, 0, 100);
        roster.TryField(MatchSide.West, "y", 1, 0, 100);

        var all = roster.Heroes;

        Assert.AreEqual(2, all.Count);
        Assert.AreEqual("x", all[0].HeroId);
        Assert.AreEqual("y", all[1].HeroId);
    }
}
