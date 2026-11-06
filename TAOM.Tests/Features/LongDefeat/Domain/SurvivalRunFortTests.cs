using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>
/// The fortification buys of phase3-plan.md (Slice 2): every result of every <c>TryBuy*</c>, the price factor of D29, the cap rule for posts (D20), field care once per intermission (D21),
/// the gate arithmetic (D17, D18), the purchase list the report reads, and the empty <see cref="FortOffer.Offered"/> of D31 that keeps a shipped run exactly as it was.
/// </summary>
[TestClass]
public class SurvivalRunFortTests
{
    private static readonly GatePlan Gate = new(200, new[] { new GateLevelPlan(50f, 200), new GateLevelPlan(100f, 300), new GateLevelPlan(150f, 450) });

    private static readonly TowerPlan Towers = new("fow_rohan_eastfold_bowman",
        new[] { new TowerLevelPlan(4, false, 250), new TowerLevelPlan(8, false, 300), new TowerLevelPlan(8, true, 400) },
        new TowerVolleyPlan("fow_tower_arrow", 6, 20f, 30f, 6f, 80f, 2.5f));

    private static readonly CarePlan Care = new(0.75f, 150, 1);

    private static readonly VolleyPlan Volley = new("ld_elven_arrow", 3f,
        new[] { new VolleyLevelPlan(350, 12, 45f, 8f, 90f, VolleyAimMode.Auto), new VolleyLevelPlan(300, 16, 40f, 8f, 90f, VolleyAimMode.Crosshair) });

    private static readonly FortPlan Everything = new(Gate, Towers, Care, Volley);

    /// <summary>What the code could deliver once every slice has landed.</summary>
    private static readonly FortOffer All = new(3, 3, true, 2);

    private static MusterFrame Post(float x) => new(x, 0f, 0f, 0f, 1f);

    private static IReadOnlyList<LdTower> TwoTowers(int postsEach = 8) => new[]
    {
        new LdTower("hornburg", Enumerable.Range(0, postsEach).Select(i => Post(i)).ToList(), new[] { new LdPoint(1f, 2f, 3f) }),
        new LdTower("wall", Enumerable.Range(0, postsEach).Select(i => Post(i)).ToList(), new[] { new LdPoint(4f, 5f, 6f) }),
    };

    private static BoonRow Boon(string id, params (string Key, float Value)[] effects) =>
        new(id, BoonCategory.WarChest, "{=K_" + id + "}" + id, "{=T_" + id + "}text", effects.Select(e => new BoonEffect(e.Key, e.Value)).ToList());

    private static RunPlan Plan(FortPlan? fort = null, FortOffer? offered = null, IReadOnlyList<LdTower>? towers = null, bool origins = true, int gold = 5000,
        RunRules? rules = null, BoonPlan? boons = null, int? stopAfter = null, int? startGold = null) =>
        new(
            "helms",
            new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30) },
            new WavePlan(new[] { new WaveSpec(20, new[] { new WaveTroop("orc", FormationSlot.Infantry, 3), new WaveTroop("skirm", FormationSlot.Ranged, 1) }) }, new WaveScaling(10, 300)),
            new ShopPlan(gold, new[] { new ShopOffer("militia", "Militia", "fow_militia", FormationSlot.Infantry, 12, 180) }, fort ?? Everything),
            rules ?? RunRules.Default with { PreparationSeconds = 50f, IntermissionSeconds = 10f },
            new RunOptions { Seed = 1UL, StopAfterWaves = stopAfter, StartGold = startGold })
        {
            Bounties = new Dictionary<string, int> { ["orc"] = 4, ["skirm"] = 6 },
            Offered = offered ?? All,
            Towers = towers ?? TwoTowers(),
            Volley = origins ? new LdVolleyOrigins(new[] { new LdPoint(0f, 0f, 0f) }) : null,
            Boons = boons ?? BoonPlan.None,
        };

    /// <summary>The preparation lasts one tick, so the second tick is already a wave.</summary>
    private static RunRules Fast() => RunRules.Default with { PreparationSeconds = 1f, IntermissionSeconds = 10f };

    /// <summary>A tick that reads the gates given, or one whole gate when none is given: a fortress with no gate is <see cref="NoGateSnap"/>.</summary>
    private static RunSnapshot Snap(int defenders = 40, params LdGateFacts[] gates) => new(1f, 0, 0, defenders, 0, 0, 1000) { Gates = gates.Length > 0 ? gates : new[] { Whole() } };

    private static RunSnapshot NoGateSnap() => new(1f, 0, 0, 40, 0, 0, 1000) { Gates = Array.Empty<LdGateFacts>() };

    private static LdGateFacts Whole() => new(0, 15000f, 15000f, 15000f, false, false);

    private static LdGateFacts Dented(float share) => new(1, 15000f * (1f - share), 15000f, 15000f, false, false);

    private static LdGateFacts Broken() => new(4, 0f, 15000f, 15000f, true, false);

    private static SurvivalRun Begun(RunPlan plan)
    {
        var run = new SurvivalRun(plan);
        run.Begin();
        return run;
    }

    private static RunEffects TickOnce(SurvivalRun run, RunSnapshot? snap = null) => run.Tick(snap ?? Snap());

    /// <summary>Ticks until <paramref name="clears"/> waves are cleared, sounding the horn after each clear; stops on the clear tick and returns the effects of that tick.</summary>
    private static RunEffects ClearTo(SurvivalRun run, int clears, Func<RunSnapshot>? snap = null)
    {
        RunEffects last = new();
        for (var i = 0; i < 5000 && run.WavesCleared < clears && run.Result == null; i++)
        {
            if (run.GetView().Phase == RunPhase.Intermission)
                run.TryStartWave();
            last = run.Tick(snap?.Invoke() ?? Snap());
        }

        Assert.AreEqual(clears, run.WavesCleared, "the run never reached the clear");
        return last;
    }

    private static SurvivalRun InIntermission(RunPlan plan, Func<RunSnapshot>? snap = null)
    {
        var run = Begun(plan);
        ClearTo(run, 1, snap);
        Assert.AreEqual(RunPhase.Intermission, run.Phase);
        return run;
    }

    // ---- D31: nothing is for sale until a slice lands its effect ----

    [TestMethod]
    public void EveryBuy_WithNothingOffered_IsNotOffered_AndSpendsNothing()
    {
        var run = Begun(Plan(offered: FortOffer.None, gold: 5000));

        Assert.AreEqual(FortResult.NotOffered, run.TryBuyGateLevel());
        Assert.AreEqual(FortResult.NotOffered, run.TryRepairGates());
        Assert.AreEqual(FortResult.NotOffered, run.TryBuyTowerLevel());
        Assert.AreEqual(FortResult.NotOffered, run.TryFieldCare());
        Assert.AreEqual(FortResult.NotOffered, run.TryBuyVolleyLevel());

        Assert.AreEqual(5000, run.Gold);
        Assert.AreEqual(0, run.FortSummary().Purchases.Count);
    }

    [TestMethod]
    public void TheShippedOffered_IsTheGateFieldCareTheVolleyAndTheFirstTwoTowerLevels_AndNoneIsEmpty()
    {
        Assert.AreEqual(new FortOffer(3, 2, true, 2), FortOffer.Offered, "Slice 4 landed field care's effect, Slice 5 the gate's three levels and its repair, Slice 6 the Elven Volley's two levels and Slice 7 the first two tower levels; the tower volley waits for Slice 8");
        Assert.AreEqual(new FortOffer(0, 0, false, 0), FortOffer.None);
        Assert.AreEqual(FortOffer.None, Plan().Offered with { Gate = 0, Towers = 0, FieldCare = false, Volley = 0 });
        var bare = new RunPlan("h", Array.Empty<GarrisonRow>(), new WavePlan(new[] { new WaveSpec(1, new[] { new WaveTroop("orc", FormationSlot.Infantry, 1) }) }, new WaveScaling(1, 1)),
            new ShopPlan(0, Array.Empty<ShopOffer>()), RunRules.Default, RunOptions.Default);
        Assert.AreEqual(FortOffer.Offered, bare.Offered, "a plan that sets nothing sells what this build can deliver");
    }

    [TestMethod]
    public void WithTheShippedOffered_OnlyTheTowerVolleyLevelIsStillNotForSale()
    {
        var run = InIntermission(Plan(offered: FortOffer.Offered, gold: 5000), () => Snap(40, Dented(0.5f)));

        Assert.AreEqual(FortResult.Ok, run.TryBuyTowerLevel());
        Assert.AreEqual(FortResult.Ok, run.TryBuyTowerLevel());
        Assert.AreEqual(FortResult.NotOffered, run.TryBuyTowerLevel(), "level 3 is the tower volley, Slice 8");
        Assert.AreEqual(FortResult.Ok, run.TryBuyVolleyLevel());
        Assert.AreEqual(FortResult.Ok, run.TryBuyGateLevel());
        Assert.AreEqual(FortResult.Ok, run.TryRepairGates());
        Assert.AreEqual(FortResult.Ok, run.TryFieldCare());
    }

    [TestMethod]
    public void TheGate_IsOfferedThroughAllThreeLevels_WithTheShippedOffered()
    {
        var run = Begun(Plan(offered: FortOffer.Offered, gold: 5000));

        Assert.AreEqual(FortResult.Ok, run.TryBuyGateLevel());
        Assert.AreEqual(FortResult.Ok, run.TryBuyGateLevel());
        Assert.AreEqual(FortResult.Ok, run.TryBuyGateLevel());
        Assert.AreEqual(FortResult.MaxLevel, run.TryBuyGateLevel());
    }

    [TestMethod]
    public void ALevelBeyondOffered_IsNotOffered_AndOneBeyondTheShopRows_IsMaxLevel()
    {
        var run = Begun(Plan(offered: new FortOffer(1, 0, false, 0)));

        Assert.AreEqual(FortResult.Ok, run.TryBuyGateLevel());
        Assert.AreEqual(FortResult.NotOffered, run.TryBuyGateLevel(), "level 2 exists in shop.json but this build cannot deliver it");

        var full = Begun(Plan(offered: All));
        full.TryBuyGateLevel();
        full.TryBuyGateLevel();
        full.TryBuyGateLevel();
        Assert.AreEqual(FortResult.MaxLevel, full.TryBuyGateLevel());
    }

    [TestMethod]
    public void ARowTheShopDoesNotCarry_IsHidden()
    {
        var run = Begun(Plan(fort: FortPlan.None));

        Assert.AreEqual(FortResult.Hidden, run.TryBuyGateLevel());
        Assert.AreEqual(FortResult.Hidden, run.TryRepairGates());
        Assert.AreEqual(FortResult.Hidden, run.TryBuyTowerLevel());
        Assert.AreEqual(FortResult.Hidden, run.TryFieldCare());
        Assert.AreEqual(FortResult.Hidden, run.TryBuyVolleyLevel());
    }

    [TestMethod]
    public void TheGateLevel_WhenTheFieldReadsNoGateToWriteTo_IsHidden_AndCostsNothing()
    {
        var run = Begun(Plan(offered: FortOffer.Offered, gold: 5000));
        run.Tick(NoGateSnap());

        Assert.AreEqual(FortResult.Hidden, run.TryBuyGateLevel(), "a fortress whose visible gates have no destructible would sell a level that fails with gates=0");
        Assert.AreEqual(5000, run.Gold);
        Assert.IsFalse(run.GetView().Fort.Gate.Available);
        Assert.AreEqual(FortResult.Hidden, run.TryRepairGates());

        run.Tick(Snap());

        Assert.AreEqual(FortResult.Ok, run.TryBuyGateLevel(), "a gate in the next read makes the row for sale again");
    }

    [TestMethod]
    public void TowersWithNothingRecorded_AndAVolleyWithNoOrigins_AreHidden()
    {
        var run = Begun(Plan(towers: Array.Empty<LdTower>(), origins: false));

        Assert.AreEqual(FortResult.Hidden, run.TryBuyTowerLevel());
        Assert.AreEqual(FortResult.Hidden, run.TryBuyVolleyLevel());
        Assert.AreEqual(FortResult.Ok, run.TryBuyGateLevel(), "the gate needs no recorded point");
    }

    // ---- the phase ----

    [TestMethod]
    public void NothingIsBought_DuringAWave_AndEverythingIsRefused_AfterTheRunEnds()
    {
        var run = Begun(Plan(rules: Fast()));
        run.Tick(Snap());
        run.Tick(Snap());
        Assert.AreEqual(RunPhase.Wave, run.Phase);

        Assert.AreEqual(FortResult.NotOpen, run.TryBuyGateLevel());
        Assert.AreEqual(FortResult.NotOpen, run.TryRepairGates());
        Assert.AreEqual(FortResult.NotOpen, run.TryBuyTowerLevel());
        Assert.AreEqual(FortResult.NotOpen, run.TryFieldCare());
        Assert.AreEqual(FortResult.NotOpen, run.TryBuyVolleyLevel());
        Assert.AreEqual(5000, run.Gold);

        run.Abandon();

        Assert.AreEqual(FortResult.RunOver, run.TryBuyGateLevel());
        Assert.AreEqual(FortResult.RunOver, run.TryFieldCare());
    }

    [TestMethod]
    public void ThePreparationPhase_IsOpenToEveryBuy()
    {
        var run = Begun(Plan());

        Assert.AreEqual(RunPhase.Preparation, run.Phase);
        Assert.AreEqual(FortResult.Ok, run.TryBuyVolleyLevel());
        Assert.AreEqual(FortResult.Ok, run.TryBuyTowerLevel());
    }

    // ---- gate levels (D18) ----

    [TestMethod]
    public void AGateLevel_OnAStandingGate_SpendsItsPrice_AndTheNextTickCarriesTheLevelledFactor()
    {
        var run = InIntermission(Plan(), () => Snap(40, Whole()));

        Assert.AreEqual(FortResult.Ok, run.TryBuyGateLevel());
        var fx = TickOnce(run, Snap(40, Whole()));

        Assert.AreEqual(1.5f, fx.GateLevel);
        Assert.IsNull(fx.RepairGates);
        Assert.AreEqual(1, run.GateLevel);
        Assert.AreEqual(5000 + EarnedAtFirstClear(run) - 200, run.Gold);

        Assert.AreEqual(FortResult.Ok, run.TryBuyGateLevel());
        Assert.AreEqual(2f, TickOnce(run, Snap(40, Whole())).GateLevel);
        Assert.IsNull(TickOnce(run, Snap(40, Whole())).GateLevel, "the factor is told once");
    }

    private static long EarnedAtFirstClear(SurvivalRun run) => run.Purse.TotalEarned;

    [TestMethod]
    public void AGateLevel_OnABrokenGate_IsBoughtTheSameWay_AndTheViewShowsTheGateBroken()
    {
        var run = InIntermission(Plan(), () => Snap(40, Broken()));

        Assert.AreEqual(FortResult.Ok, run.TryBuyGateLevel());
        var fx = TickOnce(run, Snap(40, Broken()));

        Assert.AreEqual(1.5f, fx.GateLevel, "the adapter raises only the maximum of a broken gate; the domain still tells it the level");
        Assert.IsTrue(run.GetView().Fort.Gates.Single().Destroyed);
    }

    [TestMethod]
    public void AGateLevel_WithoutTheGold_IsNoGold_AndChangesNothing()
    {
        var run = Begun(Plan(gold: 199));

        Assert.AreEqual(FortResult.NoGold, run.TryBuyGateLevel());

        Assert.AreEqual(199, run.Gold);
        Assert.AreEqual(0, run.GateLevel);
        Assert.AreEqual(0, run.FortSummary().Purchases.Count);
    }

    // ---- repairs (D17, D18) ----

    [TestMethod]
    public void ARepair_WithNoGateMissingAnyHitPoints_IsNothingToRepair()
    {
        var run = Begun(Plan());
        run.Tick(Snap(40, Whole()));

        Assert.AreEqual(FortResult.NothingToRepair, run.TryRepairGates());
        Assert.AreEqual(5000, run.Gold);
    }

    [TestMethod]
    public void ARepair_WithNoGateFacts_IsNothingToRepair()
    {
        var run = Begun(Plan());
        run.Tick(Snap());

        Assert.AreEqual(FortResult.NothingToRepair, run.TryRepairGates());
    }

    [TestMethod]
    public void ARepair_OfABrokenGate_PaysAllOfIt_AndTheNextTickCarriesTheFactor()
    {
        var run = Begun(Plan());
        run.Tick(Snap(40, Broken()));

        Assert.AreEqual(FortResult.Ok, run.TryRepairGates());
        var fx = TickOnce(run, Snap(40, Broken()));

        Assert.AreEqual(4800, run.Gold);
        Assert.AreEqual(1f, fx.RepairGates, "no level yet: the vanilla maximum");
        var bought = run.FortSummary().Purchases.Single();
        Assert.AreEqual(PurchaseKind.GateRepair, bought.Kind);
        Assert.AreEqual((200, 200, 1f), (bought.BasePrice, bought.Price, bought.Share));
    }

    [TestMethod]
    public void ARepair_OfADamagedGate_PaysTheMissingShare_AndRestoresTheLevelledMaximum()
    {
        var run = Begun(Plan());
        run.TryBuyGateLevel();
        run.Tick(Snap(40, Dented(0.37f)));

        Assert.AreEqual(FortResult.Ok, run.TryRepairGates());
        var fx = TickOnce(run, Snap(40, Dented(0.37f)));

        Assert.AreEqual(1.5f, fx.RepairGates, "a repair restores the levelled maximum");
        var bought = run.FortSummary().Purchases.Last();
        Assert.AreEqual(74, bought.Price, "ceil(200 * 0.37)");
        Assert.AreEqual(0.37f, bought.Share, 0.0005f);
        Assert.AreEqual(5000 - 200 - 74, run.Gold);
    }

    [TestMethod]
    public void ARepair_WithoutTheGold_IsNoGold()
    {
        var run = Begun(Plan(gold: 100));
        run.Tick(Snap(40, Broken()));

        Assert.AreEqual(FortResult.NoGold, run.TryRepairGates());
        Assert.AreEqual(100, run.Gold);
    }

    [TestMethod]
    public void ARepair_ChargesEachVisibleGate()
    {
        var run = Begun(Plan());
        run.Tick(Snap(40, Broken(), Dented(0.5f)));

        Assert.AreEqual(FortResult.Ok, run.TryRepairGates());

        Assert.AreEqual(5000 - 200 - 100, run.Gold);
    }

    // Found building Slice 5: the gate facts a quote reads are last tick's, so a repair bought but not yet written, or written this very tick, still reads as broken and sold twice (the second would pay for a gate that is whole).
    [TestMethod]
    public void ARepair_BoughtTwiceBeforeATick_IsSoldOnce()
    {
        var run = Begun(Plan());
        run.Tick(Snap(40, Broken()));

        Assert.AreEqual(FortResult.Ok, run.TryRepairGates());
        Assert.AreEqual(FortResult.NothingToRepair, run.TryRepairGates(), "the first is still to be written");
        Assert.IsFalse(run.GetView().Fort.Repair.Available);

        Assert.AreEqual(4800, run.Gold);
    }

    [TestMethod]
    public void ARepair_WrittenOnThisTick_IsNotSoldAgainOnTheSameTicksStaleFacts_ButIsOnceTheGatesAreReadAgain()
    {
        var run = Begun(Plan());
        run.Tick(Snap(40, Broken()));
        run.TryRepairGates();

        var fx = TickOnce(run, Snap(40, Broken()));

        Assert.IsNotNull(fx.RepairGates);
        Assert.AreEqual(FortResult.NothingToRepair, run.TryRepairGates(), "this tick's facts were read before the repair was written");
        Assert.IsFalse(run.GetView().Fort.Repair.Available);

        run.Tick(Snap(40, Dented(0.5f)));
        Assert.AreEqual(FortResult.Ok, run.TryRepairGates(), "a gate hit again is sold again once it is read again");
    }

    // Owed from the Slice 2 review (RCA row 8): the repair is priced per gate, so the purchase keeps each gate's share, and the judge replays the sum of the per-gate prices.
    [TestMethod]
    public void ARepair_OfTwoGates_RecordsEachGatesShare_AndChargesThePerGatePrices()
    {
        var fort = Everything with { Gate = new GatePlan(100, Gate.Levels) };
        var run = Begun(Plan(fort));
        run.Tick(Snap(40, Dented(0.371f), Dented(0.371f)));

        Assert.AreEqual(FortResult.Ok, run.TryRepairGates());

        var bought = run.FortSummary().Purchases.Single();
        Assert.AreEqual(76, bought.Price, "ceil(100 * 0.371) = 38 for each gate; one gate of 0.742 would cost 75");
        CollectionAssert.AreEqual(new[] { 0.371f, 0.371f }, bought.Shares!.ToArray());
        Assert.AreEqual(0.742f, bought.Share, 1e-6f);
    }

    [TestMethod]
    public void ARepair_OfABrokenAndADamagedGate_RecordsAShareAtMostOneForEach()
    {
        var run = Begun(Plan());
        run.Tick(Snap(40, Broken(), Dented(0.5f)));

        run.TryRepairGates();

        CollectionAssert.AreEqual(new[] { 1f, 0.5f }, run.FortSummary().Purchases.Single().Shares!.ToArray());
    }

    [TestMethod]
    public void ARepair_OfAGateMissingUnderHalfAThousandth_IsNothingToRepair_AndOneOverIsSoldAtOneGold()
    {
        var under = Begun(Plan());
        under.Tick(Snap(40, Dented(0.0004f)));
        Assert.AreEqual(FortResult.NothingToRepair, under.TryRepairGates(), "the report writes a share to three decimals, so a repair under 0.0005 would be written as 0 and refused by the judge");
        Assert.AreEqual(5000, under.Gold);

        var over = Begun(Plan());
        over.Tick(Snap(40, Dented(0.0006f)));
        Assert.AreEqual(FortResult.Ok, over.TryRepairGates());
        var bought = over.FortSummary().Purchases.Single();
        Assert.AreEqual(1, bought.Price);
        CollectionAssert.AreEqual(new[] { 0.001f }, bought.Shares!.ToArray(), "a share is rounded half away from zero, so 0.0006 is written 0.001, never 0");
    }

    [TestMethod]
    public void ARepair_ThenAGateLevel_BeforeATick_RestoresTheNewLevelledMaximum()
    {
        var run = Begun(Plan());
        run.Tick(Snap(40, Dented(0.37f)));

        Assert.AreEqual(FortResult.Ok, run.TryRepairGates());
        Assert.AreEqual(FortResult.Ok, run.TryBuyGateLevel());
        var fx = TickOnce(run, Snap(40, Dented(0.37f)));

        Assert.AreEqual(1.5f, fx.GateLevel);
        Assert.AreEqual(1.5f, fx.RepairGates, "the director raises the maximum first and then mends; the repair must not write the old maximum back over the new one");
    }

    // ---- the price factor (D29) ----

    [TestMethod]
    public void EveryPrice_TakesTheBoonsPriceFactor_AndNeverFallsBelowOne()
    {
        var boons = new BoonPlan(new[] { Boon("haggler", ("pricePercent", -20f)), Boon("giveaway", ("pricePercent", -100f)) }, 1);
        var run = Begun(Plan(boons: boons));
        TakeBoon(run, "haggler");

        run.Tick(Snap(40, Broken()));
        Assert.AreEqual(FortResult.Ok, run.TryBuyGateLevel());
        Assert.AreEqual(FortResult.Ok, run.TryRepairGates());
        Assert.AreEqual(FortResult.Ok, run.TryBuyVolleyLevel());

        var bought = run.FortSummary().Purchases;
        Assert.AreEqual((200, 160), (bought[0].BasePrice, bought[0].Price));
        Assert.AreEqual((200, 160), (bought[1].BasePrice, bought[1].Price), "a broken gate pays all of the factored repair price");
        Assert.AreEqual((350, 280), (bought[2].BasePrice, bought[2].Price));
    }

    [TestMethod]
    public void ARepairPrice_IsTheFactoredPrice_TimesTheMissingShare()
    {
        var boons = new BoonPlan(new[] { Boon("haggler", ("pricePercent", -20f)) }, 1);
        var run = Begun(Plan(boons: boons));
        TakeBoon(run, "haggler");
        run.Tick(Snap(40, Dented(0.37f)));

        run.TryRepairGates();

        Assert.AreEqual(60, run.FortSummary().Purchases.Single().Price, "ceil(160 * 0.37)");
    }

    [TestMethod]
    public void AFactorOfZero_StillCostsOneGold()
    {
        var boons = new BoonPlan(new[] { Boon("giveaway", ("pricePercent", -100f)) }, 1);
        var run = Begun(Plan(boons: boons));
        TakeBoon(run, "giveaway");

        Assert.AreEqual(FortResult.Ok, run.TryFieldCare());

        Assert.AreEqual(1, run.FortSummary().Purchases.Single().Price);
    }

    [TestMethod]
    public void APriceThatLandsOnAHalf_FollowsTheGamesSinglePrecisionFactor()
    {
        // 1f + -55f / 100f is 0.449999988 as a float, so 150 * 0.45 is 67.4999982 and rounds to 67; a double 0.45 would give exactly 67.5 and 68.
        // tools/tests/test_run_long_defeat.py (FortPriceTests) pins the same pair for the judge's mirror.
        var boons = new BoonPlan(new[] { Boon("tithe", ("pricePercent", -55f)) }, 1);
        var run = Begun(Plan(boons: boons));
        TakeBoon(run, "tithe");

        Assert.AreEqual(FortResult.Ok, run.TryFieldCare());

        Assert.AreEqual((150, 67), (run.FortSummary().Purchases.Last().BasePrice, run.FortSummary().Purchases.Last().Price));
    }

    private static void TakeBoon(SurvivalRun run, string id)
    {
        // A pick opens at the third clear; the test hands the deck a taken boon the quickest way the domain allows.
        ClearTo(run, 3);
        Assert.AreEqual(PickResult.Ok, run.TryPickBoon(id));
    }

    // ---- towers (D20) ----

    [TestMethod]
    public void ATowerLevel_BuysItsPosts_AndTheNextTickCarriesTheFillWithTheRoomLeft()
    {
        var run = Begun(Plan());

        Assert.AreEqual(FortResult.Ok, run.TryBuyTowerLevel());
        var fx = TickOnce(run, Snap(30));

        // The cap is 220; 30 stand; 10 of the 30-strong muster are not out yet, and they hold their room.
        Assert.AreEqual(new LdPostFill("fow_rohan_eastfold_bowman", 4, 220 - 30 - 10), fx.FillPosts);
        Assert.AreEqual(1, run.TowerLevel);
        Assert.AreEqual(4750, run.Gold);
    }

    [TestMethod]
    public void TheFill_IsToldOnce()
    {
        var run = Begun(Plan());
        run.TryBuyTowerLevel();
        Assert.IsNotNull(TickOnce(run).FillPosts);

        Assert.IsNull(TickOnce(run).FillPosts);
    }

    // The cap is 220 and 10 of the 30-strong muster are not out yet, so a snapshot of S standing leaves 210 - S for new soldiers. Two towers of 8 recorded posts: level 1 adds 4 each, 8 in all.
    private static RunRules Capped() => RunRules.Default with { PreparationSeconds = 50f, GarrisonCap = 220 };

    [TestMethod]
    public void ATowerLevel_ThatAddsMorePostsThanTheCapAllows_IsGarrisonFull_AndSpendsNothing()
    {
        var run = Begun(Plan(rules: Capped()));
        run.Tick(Snap(205));

        Assert.AreEqual(FortResult.GarrisonFull, run.TryBuyTowerLevel(), "5 left, 8 posts");

        Assert.AreEqual(5000, run.Gold);
        Assert.AreEqual(0, run.TowerLevel);
        Assert.AreEqual(0, run.FortSummary().Purchases.Count);
    }

    [TestMethod]
    public void ATowerLevel_ThatFitsExactlyUnderTheCap_IsBought()
    {
        var run = Begun(Plan(rules: Capped()));
        run.Tick(Snap(202));

        Assert.AreEqual(FortResult.Ok, run.TryBuyTowerLevel(), "8 left, 8 posts");
    }

    [TestMethod]
    public void PostsBought_CountAgainstThePackCap_UntilTheyHaveSpawned()
    {
        var run = Begun(Plan(rules: Capped()));
        run.Tick(Snap(196));
        Assert.AreEqual(FortResult.Ok, run.TryBuyTowerLevel(), "14 left, 8 posts");

        Assert.AreEqual(BuyResult.GarrisonFull, run.TryBuy("militia"), "6 left, a pack of 12; without the posts counted it would fit");
    }

    [TestMethod]
    public void APackBought_CountsAgainstThePosts()
    {
        var run = Begun(Plan(rules: Capped()));
        run.Tick(Snap(196));
        Assert.AreEqual(BuyResult.Ok, run.TryBuy("militia"));

        Assert.AreEqual(FortResult.GarrisonFull, run.TryBuyTowerLevel(), "2 left, 8 posts");
    }

    [TestMethod]
    public void ATowerWithFewerRecordedPostsThanTheLevel_CountsOnlyThoseItHas()
    {
        var run = Begun(Plan(rules: Capped(), towers: new[] { new LdTower("small", new[] { Post(0f), Post(1f) }, Array.Empty<LdPoint>()) }));
        run.Tick(Snap(208));

        Assert.AreEqual(FortResult.Ok, run.TryBuyTowerLevel(), "2 left, level 1 asks 4 posts but the tower records 2");
        Assert.AreEqual(FortResult.Ok, run.TryBuyTowerLevel(), "level 2 asks 8, still 2 recorded: nothing more to add");
        Assert.AreEqual(FortResult.Ok, run.TryBuyTowerLevel());
        Assert.AreEqual(FortResult.MaxLevel, run.TryBuyTowerLevel());
    }

    [TestMethod]
    public void TheLastTowerLevel_AddsNoPosts_SoAFullGarrisonDoesNotBlockIt()
    {
        var run = Begun(Plan(rules: Capped()));
        run.TryBuyTowerLevel();
        run.TryBuyTowerLevel();
        run.Tick(Snap(220));

        Assert.AreEqual(FortResult.Ok, run.TryBuyTowerLevel(), "level 3 is the volley: 8 posts again, so none to add");
        Assert.AreEqual(3, run.TowerLevel);
    }

    [TestMethod]
    public void AClear_RefillsThePosts_BeforeAnyPackIsBought()
    {
        var run = Begun(Plan());
        run.TryBuyTowerLevel();
        TickOnce(run);

        var fx = ClearTo(run, 1, () => Snap(100));

        Assert.IsNotNull(fx.FillPosts, "posts lost in the wave are filled again, free, at the clear");
        Assert.AreEqual(4, fx.FillPosts!.Posts);
        Assert.AreEqual(220 - 100, fx.FillPosts.Room);
        Assert.AreEqual(0, fx.DefenderBatches.Count(b => b.Reinforcement), "no pack could have been bought yet, so the refill comes first");
    }

    [TestMethod]
    public void AClear_WithNoTowerLevel_RefillsNothing()
    {
        var run = Begun(Plan());

        var fx = ClearTo(run, 1);

        Assert.IsNull(fx.FillPosts);
    }

    // ---- field care (D21) ----

    [TestMethod]
    public void FieldCare_IsBoughtOncePerIntermission_AndTheNextTickCarriesTheShare()
    {
        var run = InIntermission(Plan());

        Assert.AreEqual(FortResult.Ok, run.TryFieldCare());
        Assert.AreEqual(FortResult.Used, run.TryFieldCare());
        var fx = TickOnce(run);

        Assert.AreEqual(0.75f, fx.FieldCare);
        Assert.AreEqual(0f, TickOnce(run).FieldCare, "told once");
        var care = run.FortSummary().Purchases.Single();
        Assert.AreEqual((PurchaseKind.FieldCare, 150, 150, 0.75f), (care.Kind, care.BasePrice, care.Price, care.Share));
    }

    [TestMethod]
    public void FieldCare_InThePreparation_IsNotForSale_NobodyIsHurtBeforeTheFirstWave()
    {
        var run = Begun(Plan());

        Assert.AreEqual(FortResult.NotOffered, run.TryFieldCare());
        Assert.IsFalse(run.GetView().Fort.Care.Available, "the panel's row agrees with the buy");
        Assert.AreEqual(5000, run.Gold);
        Assert.AreEqual(0, run.FortSummary().Purchases.Count);

        ClearTo(run, 1);
        Assert.IsTrue(run.GetView().Fort.Care.Available);
        Assert.AreEqual(FortResult.Ok, run.TryFieldCare());
    }

    [TestMethod]
    public void FieldCare_IsAvailableAgainAtTheNextIntermission()
    {
        var run = InIntermission(Plan());
        run.TryFieldCare();
        ClearTo(run, 2);

        Assert.AreEqual(FortResult.Ok, run.TryFieldCare());
    }

    [TestMethod]
    public void FieldCare_PerIntermissionTwo_AllowsTwo()
    {
        var run = InIntermission(Plan(fort: Everything with { Care = new CarePlan(0.5f, 100, 2) }));

        Assert.AreEqual(FortResult.Ok, run.TryFieldCare());
        Assert.AreEqual(FortResult.Ok, run.TryFieldCare());
        Assert.AreEqual(FortResult.Used, run.TryFieldCare());
    }

    [TestMethod]
    public void FieldCare_BoughtTwiceBeforeATick_ComposesBothShares_NeitherIsLost()
    {
        var run = InIntermission(Plan(fort: Everything with { Care = new CarePlan(0.5f, 100, 2) }));

        Assert.AreEqual(FortResult.Ok, run.TryFieldCare());
        Assert.AreEqual(FortResult.Ok, run.TryFieldCare());
        var fx = TickOnce(run);

        Assert.AreEqual(0.75f, fx.FieldCare, "two heals of half the missing health leave a quarter of it missing");
    }

    [TestMethod]
    public void FieldCare_BoughtInTheIntermission_NeverSharesATickWithTheClearsRest()
    {
        var run = Begun(Plan());
        var clearTick = ClearTo(run, 1);
        Assert.IsTrue(clearTick.RestShare > 0f, "the clear's tick carries the rest");
        Assert.AreEqual(0f, clearTick.FieldCare, "care cannot have been bought before that tick returned");

        run.TryFieldCare();
        var next = TickOnce(run);

        Assert.AreEqual(0f, next.RestShare);
        Assert.AreEqual(0.75f, next.FieldCare);
    }

    // ---- a buy at the end of a stretch never rides the wave's first tick ----

    [TestMethod]
    public void AnEffectHandedOut_OnTheTickTheTimerRunsOut_HoldsTheWaveStartOneTick()
    {
        var run = Begun(Plan(rules: Fast()));
        Assert.AreEqual(FortResult.Ok, run.TryBuyGateLevel());

        var first = TickOnce(run, Snap(40, Whole()));

        Assert.AreEqual(1.5f, first.GateLevel);
        Assert.IsNull(first.StartWave, "the gates are read before this tick's effects reach them, so the wave must not record them yet");
        Assert.AreEqual(RunPhase.Preparation, run.Phase);
        Assert.IsNotNull(TickOnce(run, Snap(40, Whole())).StartWave, "the next tick starts the wave");
    }

    [TestMethod]
    public void AnEffectHandedOut_AfterTheHorn_HoldsTheWaveStartOneTick()
    {
        var run = InIntermission(Plan(), () => Snap(40, Whole()));
        Assert.AreEqual(FortResult.Ok, run.TryFieldCare());
        Assert.AreEqual(StartResult.Ok, run.TryStartWave());

        var first = TickOnce(run, Snap(40, Whole()));

        Assert.AreEqual(0.75f, first.FieldCare);
        Assert.IsNull(first.StartWave, "care is never told in a wave");
        Assert.IsNotNull(TickOnce(run, Snap(40, Whole())).StartWave);
    }

    [TestMethod]
    public void AWaveStart_WithNothingBoughtSinceTheLastTick_IsNotHeld()
    {
        var run = Begun(Plan(rules: Fast()));

        Assert.IsNotNull(TickOnce(run).StartWave, "the preparation lasts one tick: the tick that runs it out starts the wave");
    }

    // ---- the volley ----

    [TestMethod]
    public void VolleyLevels_AreBoughtInOrder_ThenMaxLevel()
    {
        var run = Begun(Plan());

        Assert.AreEqual(FortResult.Ok, run.TryBuyVolleyLevel());
        Assert.AreEqual(FortResult.Ok, run.TryBuyVolleyLevel());
        Assert.AreEqual(FortResult.MaxLevel, run.TryBuyVolleyLevel());

        var summary = run.FortSummary();
        Assert.AreEqual(2, summary.VolleyLevel);
        CollectionAssert.AreEqual(new[] { 1, 2 }, summary.Purchases.Select(p => p.Level).ToArray());
        Assert.AreEqual(5000 - 350 - 300, run.Gold);
    }

    // ---- the purchase list and the purse ----

    [TestMethod]
    public void EveryPurchase_IsListedInOrder_WithTheWaveItPrecedes_AndTheSinksSumToSpent()
    {
        var run = Begun(Plan());
        run.TryBuy("militia");
        run.TryBuyGateLevel();
        run.TryBuyTowerLevel();
        run.TryBuyVolleyLevel();
        ClearTo(run, 1);
        run.TryFieldCare();

        var bought = run.FortSummary().Purchases;

        CollectionAssert.AreEqual(
            new[] { PurchaseKind.Pack, PurchaseKind.GateLevel, PurchaseKind.TowerLevel, PurchaseKind.VolleyLevel, PurchaseKind.FieldCare },
            bought.Select(p => p.Kind).ToArray());
        Assert.IsTrue(bought.Take(4).All(p => p.BeforeWave == 1), "bought in the preparation, before wave 1");
        Assert.AreEqual(2, bought[4].BeforeWave, "care waits for the first clear");
        Assert.AreEqual("militia", bought[0].Id);
        Assert.AreEqual((180, 180), (bought[0].BasePrice, bought[0].Price));
        Assert.AreEqual(180, run.Purse.SpentOn(PurseSink.Packs));
        Assert.AreEqual(200, run.Purse.SpentOn(PurseSink.Gate));
        Assert.AreEqual(250, run.Purse.SpentOn(PurseSink.Towers));
        Assert.AreEqual(150, run.Purse.SpentOn(PurseSink.FieldCare));
        Assert.AreEqual(350, run.Purse.SpentOn(PurseSink.Volley));
        Assert.AreEqual(run.Purse.Spent, Enum.GetValues(typeof(PurseSink)).Cast<PurseSink>().Sum(run.Purse.SpentOn));
    }

    [TestMethod]
    public void APackBoughtAfterAClear_IsListedBeforeTheNextWave()
    {
        var run = InIntermission(Plan());

        run.TryBuy("militia");

        Assert.AreEqual(2, run.FortSummary().Purchases.Single().BeforeWave);
    }

    [TestMethod]
    public void APackUnderAPriceBoon_ListsTheShopsBasePrice_AndThePricePaid()
    {
        var boons = new BoonPlan(new[] { Boon("haggler", ("pricePercent", -20f)) }, 1);
        var run = Begun(Plan(boons: boons));
        TakeBoon(run, "haggler");
        run.Tick(Snap());

        run.TryBuy("militia");

        var pack = run.FortSummary().Purchases.Last();
        Assert.AreEqual((180, 144), (pack.BasePrice, pack.Price));
    }

    [TestMethod]
    public void TheFinalLevels_AreTheHighestBought()
    {
        var run = Begun(Plan());
        run.TryBuyGateLevel();
        run.TryBuyGateLevel();
        run.TryBuyTowerLevel();

        var summary = run.FortSummary();

        Assert.AreEqual((2, 1, 0), (summary.GateLevel, summary.TowerLevel, summary.VolleyLevel));
    }

    // ---- the view the HUD and the bot read ----

    [TestMethod]
    public void TheView_SaysWhichRowsAreBuyableAndAtWhatPrice()
    {
        var run = Begun(Plan(offered: new FortOffer(1, 0, true, 0)));
        run.Tick(Snap(40, Dented(0.5f)));

        var fort = run.GetView().Fort;

        Assert.AreEqual((true, 0, 3, 200), (fort.Gate.Available, fort.Gate.Level, fort.Gate.MaxLevel, fort.Gate.Price));
        Assert.AreEqual((true, 100), (fort.Repair.Available, fort.Repair.Price));
        Assert.IsFalse(fort.Towers.Available, "not offered");
        Assert.IsFalse(fort.Volley.Available);
        Assert.IsFalse(fort.Care.Available, "nobody is hurt in the preparation");
        Assert.AreEqual(1, fort.Care.MaxLevel);

        var between = InIntermission(Plan(offered: new FortOffer(1, 0, true, 0))).GetView().Fort;
        Assert.AreEqual((true, 150, 0, 1), (between.Care.Available, between.Care.Price, between.Care.Level, between.Care.MaxLevel));
    }

    [TestMethod]
    public void TheView_ShowsTheTowerRowUnavailable_WhenItsPostsWouldBustTheCap()
    {
        var run = Begun(Plan(rules: Capped()));
        run.Tick(Snap(205));

        Assert.IsFalse(run.GetView().Fort.Towers.Available);
    }

    [TestMethod]
    public void TheView_OfAShopWithNoFortifications_HasNoRows()
    {
        var view = new SurvivalRun(Plan(fort: FortPlan.None)).GetView();

        Assert.IsFalse(view.Fort.Gate.Available || view.Fort.Repair.Available || view.Fort.Towers.Available || view.Fort.Care.Available || view.Fort.Volley.Available);
    }

    [TestMethod]
    public void TheView_SaysTheBotBuysFirst_OnlyWithAStartGold()
    {
        Assert.IsFalse(new SurvivalRun(Plan()).GetView().Fort.BuysFirst);
        Assert.IsTrue(new SurvivalRun(Plan(startGold: 5000)).GetView().Fort.BuysFirst);
    }

    // ---- the gate rows of the report ----

    [TestMethod]
    public void AClearedWave_RecordsTheGateItStartedWith_AndWhetherItFell()
    {
        var run = Begun(Plan(rules: Fast()));
        run.TryBuyGateLevel();
        var calls = 0;
        ClearTo(run, 1, () => ++calls < 3 ? Snap(40, Whole()) : calls < 6 ? Snap(40, Broken()) : Snap(40, Broken()));

        var gate = run.Stats.Waves.Single().Fort.Gate;

        Assert.AreEqual(1, gate.Level);
        Assert.IsTrue(gate.Fell);
        Assert.IsNotNull(gate.FellAtSeconds);
        Assert.IsTrue(gate.StartHitPoints > 0f, "the wave began with the gate standing");
        Assert.AreEqual(15000f, gate.MaxHitPoints);
    }

    [TestMethod]
    public void AClearedWave_WithAGateThatStood_RecordsNoFall()
    {
        var run = Begun(Plan(rules: Fast()));

        ClearTo(run, 1, () => Snap(40, Whole()));

        var gate = run.Stats.Waves.Single().Fort.Gate;
        Assert.IsFalse(gate.Fell);
        Assert.IsNull(gate.FellAtSeconds);
        Assert.AreEqual(15000f, gate.StartHitPoints);
    }

    [TestMethod]
    public void AClearedWave_WithNoGateFacts_RecordsAnEmptyGateRow_AndZeroPostsAndVolleys()
    {
        var run = Begun(Plan());

        ClearTo(run, 1, NoGateSnap);

        var fort = run.Stats.Waves.Single().Fort;
        Assert.AreEqual(GateWaveRecord.None, fort.Gate);
        Assert.AreEqual(PostsWaveRecord.None, fort.Posts);
        Assert.AreEqual(VolleyWaveRecord.None, fort.Volley);
        Assert.AreEqual(VolleyWaveRecord.None, fort.TowerVolley);
    }

    // ---- the start gold ----

    [TestMethod]
    public void AStartGold_ReplacesTheShopsStartingGold()
    {
        var run = new SurvivalRun(Plan(gold: 600, startGold: 5000));

        Assert.AreEqual(5000, run.Gold);
        Assert.AreEqual(5000, run.Purse.Start);
    }

    [TestMethod]
    public void NoStartGold_KeepsTheShopsStartingGold()
    {
        Assert.AreEqual(600, new SurvivalRun(Plan(gold: 600)).Gold);
    }
}
