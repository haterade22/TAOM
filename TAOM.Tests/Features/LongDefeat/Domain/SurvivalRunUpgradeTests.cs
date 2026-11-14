using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>
/// Upgrading the soldiers the player has (#1161): a step is sold only between waves, takes every standing soldier of its troop that the gold pays for, goes out on the next tick, and is
/// booked by what the field replaced: a soldier it could not replace is paid back, so the report's purchases always sum to the sink.
/// </summary>
[TestClass]
public class SurvivalRunUpgradeTests
{
    private static readonly UpgradeStep MilitiaToAxe = new("fow_m>fow_a", "{=K_A}Axemen", "fow_m", "fow_a", 15);
    private static readonly UpgradeStep AxeToVeteran = new("fow_a>vet_a", "vet_a", "fow_a", "vet_a", 25);
    private static readonly UpgradeStep AxeToTwoHand = new("fow_a>two_a", "two_a", "fow_a", "two_a", 25);
    private static readonly UpgradeStep BowToVeteran = new("fow_b>vet_b", "vet_b", "fow_b", "vet_b", 25);

    private static RunPlan Plan(int gold = 1000, RunRules? rules = null) =>
        new(
            "helms",
            new[] { new GarrisonRow("fow_m", FormationSlot.Infantry, 30) },
            new WavePlan(new[] { new WaveSpec(20, new[] { new WaveTroop("orc", FormationSlot.Infantry, 3) }) }, new WaveScaling(10, 300)),
            new ShopPlan(gold, new[] { new ShopOffer("bowmen", "Bowmen", "fow_b", FormationSlot.Ranged, 8, 200) }) { Upgrades = new[] { MilitiaToAxe, AxeToVeteran, AxeToTwoHand, BowToVeteran } },
            rules ?? RunRules.Default with { PreparationSeconds = 50f, IntermissionSeconds = 10f },
            new RunOptions { Seed = 1UL })
        {
            Bounties = new Dictionary<string, int> { ["orc"] = 4 },
        };

    private static RunSnapshot Snap(int defenders = 40) => new(1f, 0, 0, defenders, 0, 0, 1000);

    private static SurvivalRun Begun(RunPlan plan, params (string Troop, int Count)[] standing)
    {
        var run = new SurvivalRun(plan);
        run.Begin();
        run.ObserveStanding(standing.ToDictionary(s => s.Troop, s => s.Count));
        return run;
    }

    [TestMethod]
    public void TryUpgrade_InPreparation_TakesEveryStandingSoldierTheGoldPays_AndSpendsAtOnce()
    {
        var run = Begun(Plan(gold: 1000), ("fow_m", 30));

        Assert.AreEqual(UpgradeResult.Ok, run.TryUpgrade(MilitiaToAxe.Id));

        Assert.AreEqual(1000 - 30 * 15, run.Gold);
        Assert.AreEqual(30 * 15, run.Purse.SpentOn(PurseSink.Upgrades));
        var fx = run.Tick(Snap());
        Assert.AreEqual(1, fx.Upgrades.Count);
        Assert.AreEqual(MilitiaToAxe, fx.Upgrades[0].Step);
        Assert.AreEqual(30, fx.Upgrades[0].Count);
        Assert.AreEqual(0, run.Tick(Snap()).Upgrades.Count, "an order goes out once");
    }

    [TestMethod]
    public void TryUpgrade_GoldForOnlySome_TakesThatMany()
    {
        var run = Begun(Plan(gold: 100), ("fow_m", 30));

        Assert.AreEqual(UpgradeResult.Ok, run.TryUpgrade(MilitiaToAxe.Id));

        Assert.AreEqual(100 - 6 * 15, run.Gold);
        Assert.AreEqual(6, run.Tick(Snap()).Upgrades[0].Count);
    }

    [TestMethod]
    public void TryUpgrade_NotEnoughGoldForOne_IsNoGold_AndSpendsNothing()
    {
        var run = Begun(Plan(gold: 14), ("fow_m", 30));

        Assert.AreEqual(UpgradeResult.NoGold, run.TryUpgrade(MilitiaToAxe.Id));
        Assert.AreEqual(14, run.Gold);
        Assert.AreEqual(0, run.Tick(Snap()).Upgrades.Count);
    }

    [TestMethod]
    public void TryUpgrade_NoSoldierOfThatTroop_IsNoneStanding()
    {
        var run = Begun(Plan(), ("fow_m", 30));

        Assert.AreEqual(UpgradeResult.NoneStanding, run.TryUpgrade(AxeToVeteran.Id));
        Assert.AreEqual(1000, run.Gold);
    }

    [TestMethod]
    public void TryUpgrade_AnUnknownStep_IsUnknownStep()
    {
        var run = Begun(Plan(), ("fow_m", 30));

        Assert.AreEqual(UpgradeResult.UnknownStep, run.TryUpgrade("nope"));
    }

    [TestMethod]
    public void TryUpgrade_InAWave_IsNotOpen()
    {
        var run = Begun(Plan(rules: RunRules.Default with { PreparationSeconds = 1f, IntermissionSeconds = 10f }), ("fow_m", 30));
        run.Tick(Snap());
        Assert.AreEqual(RunPhase.Wave, run.Phase);

        Assert.AreEqual(UpgradeResult.NotOpen, run.TryUpgrade(MilitiaToAxe.Id));
    }

    [TestMethod]
    public void TryUpgrade_AfterTheRunEnded_IsRunOver()
    {
        var run = Begun(Plan(), ("fow_m", 30));
        run.Abandon();

        Assert.AreEqual(UpgradeResult.RunOver, run.TryUpgrade(MilitiaToAxe.Id));
    }

    [TestMethod]
    public void TryUpgrade_Twice_BeforeTheOrderGoesOut_NeverSellsTheSameSoldierTwice()
    {
        var run = Begun(Plan(gold: 1000), ("fow_m", 30));

        Assert.AreEqual(UpgradeResult.Ok, run.TryUpgrade(MilitiaToAxe.Id));
        Assert.AreEqual(UpgradeResult.NoneStanding, run.TryUpgrade(MilitiaToAxe.Id));
        Assert.AreEqual(1000 - 30 * 15, run.Gold);
    }

    [TestMethod]
    public void CompleteUpgrade_EveryoneReplaced_BooksOnePurchaseOfTheWholeOrder_AndMovesTheCounts()
    {
        var run = Begun(Plan(gold: 1000), ("fow_m", 30));
        run.TryUpgrade(MilitiaToAxe.Id);
        var order = run.Tick(Snap()).Upgrades[0];

        run.CompleteUpgrade(order, 30);

        var purchase = run.FortSummary().Purchases.Single(p => p.Kind == PurchaseKind.Upgrade);
        Assert.AreEqual(MilitiaToAxe.Id, purchase.Id);
        Assert.AreEqual(15, purchase.BasePrice);
        Assert.AreEqual(30 * 15, purchase.Price);
        Assert.AreEqual(30, purchase.Count);
        Assert.AreEqual(1, purchase.BeforeWave);
        var rows = run.GetView().Upgrades;
        Assert.IsFalse(rows.Any(r => r.Step == MilitiaToAxe), "no militia stands any more");
        Assert.AreEqual(30, rows.Single(r => r.Step == AxeToVeteran).Standing, "the new axemen can take the next step");
    }

    [TestMethod]
    public void CompleteUpgrade_FewerReplaced_PaysTheRestBack_SoThePurchaseEqualsTheSink()
    {
        var run = Begun(Plan(gold: 1000), ("fow_m", 30));
        run.TryUpgrade(MilitiaToAxe.Id);
        var order = run.Tick(Snap()).Upgrades[0];

        run.CompleteUpgrade(order, 28);

        Assert.AreEqual(1000 - 28 * 15, run.Gold);
        Assert.AreEqual(28 * 15, run.Purse.SpentOn(PurseSink.Upgrades));
        Assert.AreEqual(28 * 15, run.Purse.Spent);
        Assert.AreEqual(28 * 15, run.FortSummary().Purchases.Single().Price);
        Assert.AreEqual(run.Purse.Start + run.Purse.TotalEarned - run.Purse.Spent, run.Gold, "the balance rule still holds");
    }

    [TestMethod]
    public void CompleteUpgrade_NobodyReplaced_BooksNoPurchase_AndPaysEverythingBack()
    {
        var run = Begun(Plan(gold: 1000), ("fow_m", 30));
        run.TryUpgrade(MilitiaToAxe.Id);
        var order = run.Tick(Snap()).Upgrades[0];

        run.CompleteUpgrade(order, 0);

        Assert.AreEqual(1000, run.Gold);
        Assert.AreEqual(0, run.FortSummary().Purchases.Count);
    }

    [TestMethod]
    public void CompleteUpgrade_MoreThanOrdered_CountsOnlyTheOrder()
    {
        var run = Begun(Plan(gold: 1000), ("fow_m", 30));
        run.TryUpgrade(MilitiaToAxe.Id);
        var order = run.Tick(Snap()).Upgrades[0];

        run.CompleteUpgrade(order, 99);

        Assert.AreEqual(30, run.FortSummary().Purchases.Single().Count);
        Assert.AreEqual(1000 - 30 * 15, run.Gold);
    }

    [TestMethod]
    public void View_ShowsOneRowPerStepWithSoldiersStanding_InShopOrder_WithWhatTheGoldPays()
    {
        var run = Begun(Plan(gold: 100), ("fow_m", 3), ("fow_a", 10), ("fow_b", 0));

        var rows = run.GetView().Upgrades;

        CollectionAssert.AreEqual(new[] { MilitiaToAxe, AxeToVeteran, AxeToTwoHand }, rows.Select(r => r.Step).ToList(), "both branches of the axemen show; no bowman stands");
        Assert.AreEqual(3, rows[0].Standing);
        Assert.AreEqual(3, rows[0].Affordable, "the gold pays for all three");
        Assert.AreEqual(10, rows[1].Standing);
        Assert.AreEqual(4, rows[1].Affordable, "100 gold pays four at 25");
    }

    [TestMethod]
    public void View_InAWave_ShowsNoUpgrade()
    {
        var run = Begun(Plan(rules: RunRules.Default with { PreparationSeconds = 1f, IntermissionSeconds = 10f }), ("fow_m", 30));
        run.Tick(Snap());

        Assert.AreEqual(0, run.GetView().Upgrades.Count);
    }

    [TestMethod]
    public void ObserveStanding_IgnoresTroopIdCase()
    {
        var run = Begun(Plan(), ("FOW_M", 5));

        Assert.AreEqual(5, run.GetView().Upgrades.Single().Standing);
    }

    [TestMethod]
    public void AWave_ForgetsTheStandingCounts_SoTheNextIntermissionSellsNobodyUntilTheFieldIsReadAgain()
    {
        var run = Begun(Plan(rules: RunRules.Default with { PreparationSeconds = 1f, IntermissionSeconds = 10f }), ("fow_m", 30));
        for (var i = 0; i < 5000 && run.WavesCleared < 1 && run.Result == null; i++)
            run.Tick(Snap());
        Assert.AreEqual(RunPhase.Intermission, run.Phase);

        Assert.AreEqual(UpgradeResult.NoneStanding, run.TryUpgrade(MilitiaToAxe.Id), "the militia of the preparation may have died in the wave");
        Assert.AreEqual(0, run.GetView().Upgrades.Count);

        run.ObserveStanding(new Dictionary<string, int> { ["fow_m"] = 12 });
        Assert.AreEqual(12, run.GetView().Upgrades.Single().Standing);
    }

    [TestMethod]
    public void Order_CarriesTheTargetTroopsHealthFactor()
    {
        var run = Begun(Plan(), ("fow_b", 8));
        run.TryUpgrade(BowToVeteran.Id);

        var order = run.Tick(Snap()).Upgrades.Single();

        Assert.AreEqual(1f, order.HealthFactor);
    }

    [TestMethod]
    public void AnOrderBoughtOnTheLastTickOfACalm_HoldsTheWaveATick_AndIsBookedForThatWave()
    {
        var run = Begun(Plan(rules: RunRules.Default with { PreparationSeconds = 1f, IntermissionSeconds = 10f }), ("fow_m", 30));
        Assert.AreEqual(UpgradeResult.Ok, run.TryUpgrade(MilitiaToAxe.Id));

        var fx = run.Tick(Snap());

        var order = fx.Upgrades.Single();
        Assert.AreEqual(1, order.BeforeWave);
        Assert.AreEqual(RunPhase.Preparation, run.Phase, "the timer ran out, but the wave waits for the order");
        Assert.IsNull(fx.StartWave);
        run.CompleteUpgrade(order, 30);
        Assert.AreEqual(1, run.FortSummary().Purchases.Single(p => p.Kind == PurchaseKind.Upgrade).BeforeWave);
        run.Tick(Snap());
        Assert.AreEqual(RunPhase.Wave, run.Phase);
    }

    [TestMethod]
    public void AnOrderBeforeTheHorn_RunsBeforeTheWavesFirstBatch_AndIsBookedForThatWave()
    {
        var run = Begun(Plan(), ("fow_m", 30));
        run.TryUpgrade(MilitiaToAxe.Id);
        Assert.AreEqual(StartResult.Ok, run.TryStartWave());

        var fx = run.Tick(Snap());

        Assert.AreEqual(1, fx.Upgrades.Single().BeforeWave);
        Assert.IsNull(fx.StartWave, "the wave's first batch waits a tick for the order");
        Assert.AreEqual(0, fx.AttackerBatches.Count);
        run.CompleteUpgrade(fx.Upgrades[0], 30);
        Assert.AreEqual(1, run.FortSummary().Purchases.Single(p => p.Kind == PurchaseKind.Upgrade).BeforeWave);
        Assert.IsNotNull(run.Tick(Snap()).StartWave);
    }

    [TestMethod]
    public void Order_ReadsTheHealthFactorWhenItGoesOut_SoABoonPickedAfterTheBuyCounts()
    {
        var boon = new BoonRow("hardy", BoonCategory.WarChest, "{=K_h}h", "{=T_h}h", new[] { new BoonEffect(BoonKeys.HealthPercent, 50f, BoonTarget.Archers) });
        var plan = Plan(gold: 5000, rules: RunRules.Default with { PreparationSeconds = 1f, IntermissionSeconds = 10f }) with { Boons = new BoonPlan(new[] { boon }, 1) };
        var run = Begun(plan);
        for (var i = 0; i < 5000 && run.GetView().BoonOffer.Count == 0 && run.Result == null; i++)
        {
            if (run.GetView().Phase == RunPhase.Intermission)
                run.TryStartWave();
            run.Tick(Snap());
        }

        Assert.AreEqual(RunPhase.Intermission, run.Phase, "the run never reached a boon offer");
        run.ObserveStanding(new Dictionary<string, int> { ["fow_b"] = 8 });
        Assert.AreEqual(UpgradeResult.Ok, run.TryUpgrade(BowToVeteran.Id));
        Assert.AreEqual(PickResult.Ok, run.TryPickBoon("hardy"));

        var order = run.Tick(Snap()).Upgrades.Single();

        Assert.AreEqual(1.5f, order.HealthFactor, 1e-4f);
    }

    [TestMethod]
    public void TryUpgrade_EmitsABoughtEvent_ThatNamesTheStepAndTheCount()
    {
        var run = Begun(Plan(gold: 1000), ("fow_m", 30));
        run.TryUpgrade(MilitiaToAxe.Id);

        var fx = run.Tick(Snap());

        Assert.IsTrue(fx.Events.Any(e => e.Kind == RunEventKind.Bought && e.Detail == "upgrade=fow_m>fow_a n=30"));
    }
}
