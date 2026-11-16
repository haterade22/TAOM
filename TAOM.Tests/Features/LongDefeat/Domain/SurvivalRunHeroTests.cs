using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>
/// A hero bought at the shop (#1325): sold only between waves, one of each alive, spawned on the next tick, counted against the garrison
/// cap, and for sale again once he has fallen. A spawn the field refuses is paid back, so the report's purchases always sum to the sink.
/// </summary>
[TestClass]
public class SurvivalRunHeroTests
{
    private static readonly HeroOffer Aragorn = new("aragorn", "{=K_A}Aragorn", "{=T_A}text", "taom_hero_aragorn", 300, 3f);
    private static readonly HeroOffer Gimli = new("gimli", "{=K_G}Gimli", "{=T_G}text", "taom_hero_gimli", 250, 2.5f);

    private static RunPlan Plan(int gold = 1000, RunRules? rules = null) =>
        new(
            "helms",
            new[] { new GarrisonRow("fow_m", FormationSlot.Infantry, 30) },
            new WavePlan(new[] { new WaveSpec(20, new[] { new WaveTroop("orc", FormationSlot.Infantry, 3) }) }, new WaveScaling(10, 300)),
            new ShopPlan(gold, new[] { new ShopOffer("bowmen", "Bowmen", "fow_b", FormationSlot.Ranged, 8, 200) }) { Heroes = new[] { Aragorn, Gimli } },
            rules ?? RunRules.Default with { PreparationSeconds = 50f, IntermissionSeconds = 10f },
            new RunOptions { Seed = 1UL })
        {
            Bounties = new Dictionary<string, int> { ["orc"] = 4 },
        };

    private static RunSnapshot Snap(int defenders = 40) => new(1f, 0, 0, defenders, 0, 0, 1000);

    private static SurvivalRun Begun(RunPlan plan)
    {
        var run = new SurvivalRun(plan);
        run.Begin();
        return run;
    }

    private static HeroState StateOf(SurvivalRun run, HeroOffer hero) => run.GetView().Heroes.Single(h => h.Offer.Id == hero.Id).State;

    [TestMethod]
    public void TryBuyHero_InPreparation_SpendsAtOnce_AndTheNextTickCarriesOneSpawn()
    {
        var run = Begun(Plan());

        Assert.AreEqual(HeroResult.Ok, run.TryBuyHero("aragorn"));

        Assert.AreEqual(700, run.Gold);
        Assert.AreEqual(300, run.Purse.SpentOn(PurseSink.Heroes));
        Assert.AreEqual(HeroState.Pending, StateOf(run, Aragorn));
        var fx = run.Tick(Snap());
        Assert.AreEqual(new HeroSpawn("aragorn", "taom_hero_aragorn", 3f), fx.SpawnHeroes.Single());
        Assert.AreEqual(0, run.Tick(Snap()).SpawnHeroes.Count, "a spawn goes out once");
    }

    [TestMethod]
    public void TryBuyHero_BooksAHeroPurchaseForTheWaveThatStartsNext()
    {
        var run = Begun(Plan());

        run.TryBuyHero("gimli");

        var bought = run.FortSummary().Purchases.Single();
        Assert.AreEqual((PurchaseKind.Hero, "gimli", 1, 250, 250), (bought.Kind, bought.Id, bought.BeforeWave, bought.BasePrice, bought.Price));
    }

    [TestMethod]
    public void TryBuyHero_AnUnknownHero_IsRefused()
    {
        Assert.AreEqual(HeroResult.UnknownHero, Begun(Plan()).TryBuyHero("boromir"));
    }

    [TestMethod]
    public void TryBuyHero_InAWave_IsNotOpen()
    {
        var run = Begun(Plan());
        run.TryStartWave();
        run.Tick(Snap());
        var gold = run.Gold;

        Assert.AreEqual(HeroResult.NotOpen, run.TryBuyHero("aragorn"));
        Assert.AreEqual(gold, run.Gold, "the horn paid its bonus; the refused buy took nothing");
    }

    [TestMethod]
    public void TryBuyHero_ShortOfGold_IsNoGold_AndSpendsNothing()
    {
        var run = Begun(Plan(gold: 299));

        Assert.AreEqual(HeroResult.NoGold, run.TryBuyHero("aragorn"));

        Assert.AreEqual(299, run.Gold);
        Assert.AreEqual(HeroState.ForSale, StateOf(run, Aragorn));
    }

    [TestMethod]
    public void TryBuyHero_BoughtButNotYetOnTheField_IsAlive()
    {
        var run = Begun(Plan());
        run.TryBuyHero("aragorn");

        Assert.AreEqual(HeroResult.Alive, run.TryBuyHero("aragorn"));
        Assert.AreEqual(700, run.Gold, "one Aragorn, paid once");
    }

    [TestMethod]
    public void TryBuyHero_WhileHeStands_IsAlive_AndTheOtherHeroIsStillForSale()
    {
        var run = Begun(Plan());
        run.TryBuyHero("aragorn");
        run.Tick(Snap());
        run.HeroSpawned("aragorn", true);

        Assert.AreEqual(HeroState.Standing, StateOf(run, Aragorn));
        Assert.AreEqual(HeroResult.Alive, run.TryBuyHero("aragorn"));
        Assert.AreEqual(HeroResult.Ok, run.TryBuyHero("gimli"));
    }

    [TestMethod]
    public void TryBuyHero_AtTheGarrisonCap_IsGarrisonFull()
    {
        // 30 stand and the cap is 30: a hero is one more defender.
        var run = Begun(Plan(rules: RunRules.Default with { PreparationSeconds = 50f, GarrisonCap = 30, MusterBatch = 30 }));
        run.Tick(Snap(defenders: 30));

        Assert.AreEqual(HeroResult.GarrisonFull, run.TryBuyHero("aragorn"));
        Assert.AreEqual(1000, run.Gold);
    }

    [TestMethod]
    public void APendingHero_HoldsHisRoomUnderTheCap()
    {
        var run = Begun(Plan());
        var before = run.PendingDefenders;

        run.TryBuyHero("aragorn");

        Assert.AreEqual(before + 1, run.PendingDefenders);
        run.Tick(Snap());
        run.HeroSpawned("aragorn", true);
        Assert.AreEqual(before, run.PendingDefenders, "on the field he is counted with the standing defenders");
    }

    [TestMethod]
    public void HeroSpawned_Refused_PaysTheGoldBack_DropsThePurchase_AndSellsHimAgain()
    {
        var run = Begun(Plan());
        run.TryBuyHero("aragorn");
        run.Tick(Snap());

        run.HeroSpawned("aragorn", false);

        Assert.AreEqual(1000, run.Gold);
        Assert.AreEqual(0, run.Purse.SpentOn(PurseSink.Heroes));
        Assert.AreEqual(0, run.FortSummary().Purchases.Count, "the purchases must sum to the sink");
        Assert.AreEqual(HeroState.ForSale, StateOf(run, Aragorn));
        Assert.AreEqual(HeroResult.Ok, run.TryBuyHero("aragorn"));
    }

    [TestMethod]
    public void TryBuyHero_AfterTheRunEnded_IsRunOver_AndTakesNothing()
    {
        var run = Begun(Plan());
        run.Abandon();

        Assert.AreEqual(HeroResult.RunOver, run.TryBuyHero("aragorn"));
        Assert.AreEqual(1000, run.Gold);
        Assert.AreEqual(0, run.FortSummary().Purchases.Count);
    }

    [TestMethod]
    public void TryBuyHero_QueuesOneBoughtEvent_NamingTheHero()
    {
        var run = Begun(Plan());

        run.TryBuyHero("aragorn");

        var events = run.Tick(Snap()).Events.Where(e => e.Kind == RunEventKind.Bought).ToList();
        Assert.AreEqual("hero=aragorn", events.Single().Detail);
        Assert.AreEqual(0, run.Tick(Snap()).Events.Count(e => e.Kind == RunEventKind.Bought), "an event goes out once");
    }

    [TestMethod]
    public void TryBuyHero_ARefusedBuy_QueuesNoBoughtEvent()
    {
        var run = Begun(Plan(gold: 299));

        run.TryBuyHero("aragorn");

        Assert.AreEqual(0, run.Tick(Snap()).Events.Count(e => e.Kind == RunEventKind.Bought));
    }

    [TestMethod]
    public void HeroSpawned_RefusedOnASecondBuy_DropsOnlyTheLastPurchase_AndPaysItsPriceBack()
    {
        var run = Begun(Plan());
        run.TryBuyHero("aragorn");
        run.Tick(Snap());
        run.HeroSpawned("aragorn", true);
        run.HeroDown("aragorn", loss: true);
        Assert.AreEqual(HeroResult.Ok, run.TryBuyHero("aragorn"));
        Assert.AreEqual(400, run.Gold);
        run.Tick(Snap());

        run.HeroSpawned("aragorn", false);

        Assert.AreEqual(700, run.Gold, "the second price came back, the first stays spent");
        Assert.AreEqual(300, run.Purse.SpentOn(PurseSink.Heroes));
        Assert.AreEqual(1, run.FortSummary().Purchases.Count(p => p.Kind == PurchaseKind.Hero && p.Id == "aragorn"));
        Assert.AreEqual(HeroState.ForSale, StateOf(run, Aragorn));
    }

    [TestMethod]
    public void HeroSpawned_ForAHeroNobodyBought_ChangesNothing()
    {
        var run = Begun(Plan());

        run.HeroSpawned("aragorn", false);
        run.HeroSpawned("aragorn", true);

        Assert.AreEqual(1000, run.Gold);
        Assert.AreEqual(HeroState.ForSale, StateOf(run, Aragorn));
    }

    [TestMethod]
    public void HeroDown_AFall_SellsHimAgain_AndCountsOneDefenderLoss()
    {
        var run = Begun(Plan());
        run.TryBuyHero("aragorn");
        run.Tick(Snap());
        run.HeroSpawned("aragorn", true);
        var lost = run.Stats.LostThisWave;

        run.HeroDown("aragorn", loss: true);

        Assert.AreEqual(HeroState.ForSale, StateOf(run, Aragorn));
        Assert.AreEqual(lost + 1, run.Stats.LostThisWave);
        Assert.AreEqual(HeroResult.Ok, run.TryBuyHero("aragorn"), "he is bought back in the same calm");
        Assert.AreEqual(2, run.FortSummary().Purchases.Count(p => p.Kind == PurchaseKind.Hero && p.Id == "aragorn"));
    }

    [TestMethod]
    public void HeroDown_ARout_SellsHimAgain_WithNoDefenderLoss()
    {
        var run = Begun(Plan());
        run.TryBuyHero("aragorn");
        run.Tick(Snap());
        run.HeroSpawned("aragorn", true);
        var lost = run.Stats.LostThisWave;

        run.HeroDown("aragorn", loss: false);

        Assert.AreEqual(HeroState.ForSale, StateOf(run, Aragorn));
        Assert.AreEqual(lost, run.Stats.LostThisWave);
    }

    [TestMethod]
    public void HeroDown_ForAHeroNotOnTheField_ChangesNothing()
    {
        var run = Begun(Plan());
        var lost = run.Stats.LostThisWave;

        run.HeroDown("aragorn", loss: true);

        Assert.AreEqual(lost, run.Stats.LostThisWave);
    }

    [TestMethod]
    public void TheView_ListsEveryHeroOfTheShop_InItsOrder_WithThePriceToPay()
    {
        var rows = Begun(Plan()).GetView().Heroes;

        CollectionAssert.AreEqual(new[] { "aragorn", "gimli" }, rows.Select(r => r.Offer.Id).ToArray());
        CollectionAssert.AreEqual(new[] { 300, 250 }, rows.Select(r => r.Price).ToArray());
        Assert.IsTrue(rows.All(r => r.State == HeroState.ForSale));
    }

    [TestMethod]
    public void AShopWithNoHeroes_ListsNone_AndRefusesEveryId()
    {
        var plan = Plan();
        var run = Begun(plan with { Shop = plan.Shop with { Heroes = System.Array.Empty<HeroOffer>() } });

        Assert.AreEqual(0, run.GetView().Heroes.Count);
        Assert.AreEqual(HeroResult.UnknownHero, run.TryBuyHero("aragorn"));
    }

    [TestMethod]
    public void TheLaunchCheck_KnowsTheHeroTroops()
    {
        CollectionAssert.IsSubsetOf(new[] { "taom_hero_aragorn", "taom_hero_gimli" }, RunPlanTroops.AllTroopIds(Plan()).ToArray());
        // Boons draw for the troops of the defender groups; a hero is nobody's group.
        Assert.IsFalse(RunPlanTroops.DefenderTroopIds(Plan()).Contains("taom_hero_aragorn"));
    }
}
