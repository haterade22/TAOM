using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;
using static TAOM.Tests.Features.FordsOfWar.Domain.FordsMatchFixture;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>
/// A bought-back hero holds its alive slot from the purchase, and a buyback is refused once the respawn timer has ended.
/// West owns hero_a and hero_b from the fixture plus a third offer hero_c, with room for two alive heroes.
/// </summary>
[TestClass]
public class FordsMatchBuybackSlotTests
{
    private static FordsMatch ThreeHeroMatch()
    {
        var factions = Factions().ToDictionary(
            p => p.Key,
            p => p.Value with { Heroes = p.Value.Heroes.Append(new HeroOffer("hero_c", 300)).ToList() });
        var config = Config(c => c with { Economy = c.Economy with { StartingGold = 5000 } });
        return new FordsMatch(config, Layout(), factions, Slots("slow"), new SeededFordsRandom(1));
    }

    private static void KillHero(FordsMatch match, int key, string heroId)
    {
        match.RegisterUnit(key, Unit(key, MatchSide.West, WestSlot, role: UnitRole.Hero, heroId: heroId));
        match.OnUnitKilled(Key(key), null);
    }

    [TestMethod]
    public void TryBuyback_ThenTryBuyHero_TheBoughtBackHeroHoldsTheSlot()
    {
        var match = ThreeHeroMatch();
        match.TryBuyHero(WestSlot, 0, "hero_a");
        match.TryBuyHero(WestSlot, 0, "hero_b");
        match.Tick(Snap(1f));
        KillHero(match, 10, "hero_b");

        var bought = match.TryBuyback(WestSlot, "hero_b");
        var goldAfterBuyback = match.GetView(WestSlot).Gold;
        var third = match.TryBuyHero(WestSlot, 0, "hero_c");

        Assert.AreEqual(CommandResult.Ok, bought);
        Assert.AreEqual(CommandResult.HeroLimit, third);
        Assert.AreEqual(goldAfterBuyback, match.GetView(WestSlot).Gold);
        Assert.AreEqual(0, match.GetView(WestSlot).HeroSlotsFree);
    }

    [TestMethod]
    public void Tick_AfterABuyback_SpawnsTheBoughtBackHeroAndNoOther()
    {
        var match = ThreeHeroMatch();
        match.TryBuyHero(WestSlot, 0, "hero_a");
        match.TryBuyHero(WestSlot, 0, "hero_b");
        match.Tick(Snap(1f));
        KillHero(match, 10, "hero_b");
        match.TryBuyback(WestSlot, "hero_b");
        match.TryBuyHero(WestSlot, 0, "hero_c");

        var effects = match.Tick(Snap(0.5f));

        var spawn = effects.Spawns.Single(s => s.IsHero);
        Assert.AreEqual("hero_b", spawn.TroopId);
        Assert.AreEqual(HeroState.Alive, match.GetView(WestSlot).Heroes.Single(h => h.HeroId == "hero_b").State);
        Assert.AreEqual(0, match.GetView(WestSlot).HeroSlotsFree);
    }

    [TestMethod]
    public void TryBuyback_TwiceBeforeTheTick_ChargesOnce()
    {
        var match = ThreeHeroMatch();
        match.TryBuyHero(WestSlot, 0, "hero_a");
        match.Tick(Snap(1f));
        KillHero(match, 10, "hero_a");
        match.TryBuyback(WestSlot, "hero_a");
        var gold = match.GetView(WestSlot).Gold;

        var second = match.TryBuyback(WestSlot, "hero_a");

        Assert.AreEqual(CommandResult.HeroNotDead, second);
        Assert.AreEqual(gold, match.GetView(WestSlot).Gold);
    }

    [TestMethod]
    public void TryBuyback_RespawnTimerAlreadyEnded_IsRefusedAndCostsNothing()
    {
        var match = ThreeHeroMatch();
        match.TryBuyHero(WestSlot, 0, "hero_a");
        match.TryBuyHero(WestSlot, 0, "hero_b");
        match.Tick(Snap(1f));
        KillHero(match, 10, "hero_a");
        match.TryBuyHero(WestSlot, 0, "hero_c");
        match.Tick(Snap(70f));
        var gold = match.GetView(WestSlot).Gold;

        var result = match.TryBuyback(WestSlot, "hero_a");

        Assert.AreEqual(CommandResult.HeroNotDead, result);
        Assert.AreEqual(gold, match.GetView(WestSlot).Gold);
    }

    private static bool CanBuyback(FordsMatch match, string heroId) =>
        match.GetView(WestSlot).Heroes.Single(h => h.HeroId == heroId && h.Side == MatchSide.West).CanBuyback;

    [TestMethod]
    public void GetView_DeadHeroWithTheTimerRunningAndASlotFree_CanBuybackWhateverTheGold()
    {
        var config = Config(c => c with { Economy = c.Economy with { StartingGold = 800 } });
        var match = new FordsMatch(config, Layout(), Factions(), Slots("slow"), new SeededFordsRandom(1));
        match.TryBuyHero(WestSlot, 0, "hero_a");
        match.Tick(Snap(1f));
        KillHero(match, 10, "hero_a");

        var hero = match.GetView(WestSlot).Heroes.Single(h => h.Side == MatchSide.West);

        Assert.IsTrue(hero.CanBuyback);
        Assert.IsTrue(match.GetView(WestSlot).Gold < hero.BuybackPrice, "the flag does not look at gold");
        Assert.AreEqual(CommandResult.NotEnoughGold, match.TryBuyback(WestSlot, "hero_a"));
    }

    [TestMethod]
    public void GetView_AliveHero_CannotBuyback()
    {
        var match = ThreeHeroMatch();
        match.TryBuyHero(WestSlot, 0, "hero_a");
        match.Tick(Snap(1f));

        Assert.IsFalse(CanBuyback(match, "hero_a"));
    }

    [TestMethod]
    public void GetView_HeroAlreadyBoughtBack_CannotBuybackAgain()
    {
        var match = ThreeHeroMatch();
        match.TryBuyHero(WestSlot, 0, "hero_a");
        match.Tick(Snap(1f));
        KillHero(match, 10, "hero_a");
        Assert.IsTrue(CanBuyback(match, "hero_a"));

        match.TryBuyback(WestSlot, "hero_a");

        Assert.IsFalse(CanBuyback(match, "hero_a"));
    }

    [TestMethod]
    public void GetView_RespawnTimerEnded_CannotBuyback()
    {
        var match = ThreeHeroMatch();
        match.TryBuyHero(WestSlot, 0, "hero_a");
        match.TryBuyHero(WestSlot, 0, "hero_b");
        match.Tick(Snap(1f));
        KillHero(match, 10, "hero_a");
        match.TryBuyHero(WestSlot, 0, "hero_c");
        match.Tick(Snap(70f));

        Assert.IsFalse(CanBuyback(match, "hero_a"));
    }

    [TestMethod]
    public void GetView_SideAtItsAliveLimit_CannotBuyback()
    {
        var match = ThreeHeroMatch();
        match.TryBuyHero(WestSlot, 0, "hero_a");
        match.TryBuyHero(WestSlot, 0, "hero_b");
        match.Tick(Snap(1f));
        KillHero(match, 10, "hero_a");
        match.TryBuyHero(WestSlot, 0, "hero_c");

        Assert.IsFalse(CanBuyback(match, "hero_a"));
        Assert.AreEqual(CommandResult.HeroLimit, match.TryBuyback(WestSlot, "hero_a"));
    }

    [TestMethod]
    public void GetView_AnEnemyHero_IsNeverOfferedToTheViewer()
    {
        var config = Config(c => c with { Economy = c.Economy with { StartingGold = 5000 } });
        var match = new FordsMatch(config, Layout(), Factions(), Slots("slow"), new SeededFordsRandom(1));
        match.TryBuyHero(EastSlot, 0, "hero_a");
        match.Tick(Snap(1f));
        match.RegisterUnit(1, Unit(10, MatchSide.East, EastSlot, role: UnitRole.Hero, heroId: "hero_a"));
        match.OnUnitKilled(Key(10), null);

        Assert.IsTrue(match.GetView(EastSlot).Heroes.Single().CanBuyback);
        Assert.IsFalse(match.GetView(WestSlot).Heroes.Single().CanBuyback);
    }
}
