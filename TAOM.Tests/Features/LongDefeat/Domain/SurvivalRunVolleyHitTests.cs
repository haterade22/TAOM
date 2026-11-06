using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>
/// Phase 3 Slice 6, the run's side of the engine half: the hits and kills the director books into the wave's tallies (foe hits only, apart for the Elven Volley and the towers), and the usable origins the
/// director hands back after the boot check, which can hide the volley row for a fortress whose recorded origins all failed.
/// </summary>
[TestClass]
public class SurvivalRunVolleyHitTests
{
    private static readonly VolleyPlan Volley = new("ld_elven_arrow", 3f,
        new[] { new VolleyLevelPlan(350, 12, 45f, 8f, 90f, VolleyAimMode.Auto), new VolleyLevelPlan(300, 16, 40f, 7f, 85f, VolleyAimMode.Crosshair) });

    private static readonly FortPlan Fort = new(null, null, null, Volley);
    private static readonly LdPoint[] Origins = { new(0f, 0f, 0f), new(10f, 0f, 0f), new(20f, 0f, 0f) };

    private static RunRules Rules() => RunRules.Default with { PreparationSeconds = 1f, IntermissionSeconds = 10f, QuietSeconds = 1e6f, QuietAnySeconds = 1e6f, StrandedSeconds = 1e6f };

    private static RunPlan Plan(IReadOnlyList<LdPoint>? origins = null) =>
        new(
            "helms",
            new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30) },
            new WavePlan(new[] { new WaveSpec(20, new[] { new WaveTroop("orc", FormationSlot.Infantry, 3) }) }, new WaveScaling(10, 300)),
            new ShopPlan(5000, new[] { new ShopOffer("militia", "Militia", "fow_militia", FormationSlot.Infantry, 12, 180) }, Fort),
            Rules(),
            new RunOptions { Seed = 1UL })
        {
            Bounties = new Dictionary<string, int> { ["orc"] = 4 },
            Offered = new FortOffer(0, 0, false, 2),
            Volley = new LdVolleyOrigins(origins ?? Origins),
        };

    private static RunSnapshot Fighting(int attackers = 10) => new(1f, attackers, 0, 40, 0, 0, 1000);

    private static LdVolleyHit Foe(bool kill = false) => new("ld_elven_arrow", LdVolleyHitKind.Foe, kill, false, false, 45f, 30f);

    private static LdVolleyHit Of(LdVolleyHitKind kind) => new("ld_elven_arrow", kind, false, false, false, 45f, 30f);

    private static SurvivalRun InTheWave(RunPlan? plan = null, int levels = 1)
    {
        var run = new SurvivalRun(plan ?? Plan());
        run.Begin();
        for (var i = 0; i < levels; i++)
            Assert.AreEqual(FortResult.Ok, run.TryBuyVolleyLevel());
        for (var i = 0; i < 20 && run.Phase != RunPhase.Wave; i++)
            run.Tick(Fighting());
        Assert.AreEqual(RunPhase.Wave, run.Phase);
        return run;
    }

    private static void ClearTheWave(SurvivalRun run)
    {
        var before = run.WavesCleared;
        for (var i = 0; i < 500 && run.WavesCleared == before && run.Result == null; i++)
            run.Tick(Fighting(0));
        Assert.AreEqual(before + 1, run.WavesCleared);
    }

    [TestMethod]
    public void AFoeHit_AndAKill_AreBookedInTheElvenTally_OfTheClearedWave()
    {
        var run = InTheWave();
        run.VolleyFired(VolleySource.Elven, 12);

        run.VolleyHit(VolleySource.Elven, Foe());
        run.VolleyHit(VolleySource.Elven, Foe(kill: true));
        run.VolleyHit(VolleySource.Elven, Foe(kill: true));
        ClearTheWave(run);

        var row = run.Stats.Waves.Single().Fort.Volley;
        Assert.AreEqual(3, row.Hits);
        Assert.AreEqual(2, row.Kills);
        Assert.AreEqual(1, row.Uses);
        Assert.AreEqual(12, row.Arrows);
        Assert.AreEqual(0, run.Stats.Waves.Single().Fort.TowerVolley.Hits, "the towers have their own tally");
    }

    [TestMethod]
    public void OnlyAFoeHit_IsBooked_NotABlock_AFriendlyHit_OrACancelledOne()
    {
        var run = InTheWave();
        run.VolleyFired(VolleySource.Elven, 12);

        foreach (var kind in new[] { LdVolleyHitKind.Blocked, LdVolleyHitKind.Friendly, LdVolleyHitKind.Cancelled })
            run.VolleyHit(VolleySource.Elven, Of(kind));
        ClearTheWave(run);

        var row = run.Stats.Waves.Single().Fort.Volley;
        Assert.AreEqual(0, row.Hits);
        Assert.AreEqual(0, row.Kills);
    }

    [TestMethod]
    public void ATowerHit_GoesToTheTowerTally_NotTheElvenOne()
    {
        var run = InTheWave();

        run.VolleyHit(VolleySource.Tower("hornburg"), Foe(kill: true));
        ClearTheWave(run);

        var fort = run.Stats.Waves.Single().Fort;
        Assert.AreEqual(1, fort.TowerVolley.Hits);
        Assert.AreEqual(1, fort.TowerVolley.Kills);
        Assert.AreEqual(0, fort.Volley.Hits);
    }

    [TestMethod]
    public void AHitAfterTheClear_OrBeforeTheWave_OrAfterTheRunEnded_ChangesNothing()
    {
        var run = new SurvivalRun(Plan());
        run.Begin();
        run.TryBuyVolleyLevel();
        run.VolleyHit(VolleySource.Elven, Foe());
        for (var i = 0; i < 20 && run.Phase != RunPhase.Wave; i++)
            run.Tick(Fighting());
        ClearTheWave(run);
        run.VolleyHit(VolleySource.Elven, Foe());
        run.TryStartWave();
        for (var i = 0; i < 5; i++)
            run.Tick(Fighting());
        ClearTheWave(run);

        Assert.AreEqual(0, run.Stats.Waves[0].Fort.Volley.Hits, "before the first wave");
        Assert.AreEqual(0, run.Stats.Waves[1].Fort.Volley.Hits, "the hit that landed after the first clear is nobody's");
    }

    [TestMethod]
    public void ARunThatEndsInAWave_HandsOverTheHitsItBooked()
    {
        var run = InTheWave();
        run.VolleyFired(VolleySource.Elven, 12);
        run.VolleyHit(VolleySource.Elven, Foe(kill: true));
        run.Abandon();

        Assert.AreEqual(1, run.VolleyAtEnd!.Elven.Hits);
        Assert.AreEqual(1, run.VolleyAtEnd.Elven.Kills);
    }

    [TestMethod]
    public void TheUsableOrigins_ReplaceTheRecordedOnes_InTheOrder()
    {
        var run = new SurvivalRun(Plan());
        run.Begin();
        run.LimitVolleyOrigins(new[] { Origins[2], Origins[0] });
        Assert.AreEqual(FortResult.Ok, run.TryBuyVolleyLevel());
        for (var i = 0; i < 20 && run.Phase != RunPhase.Wave; i++)
            run.Tick(Fighting());
        Assert.AreEqual(VolleyArm.Armed, run.ArmVolley());

        var order = run.Tick(Fighting()).FireVolleys.Single();

        CollectionAssert.AreEqual(new[] { Origins[2], Origins[0] }, order.Origins.ToArray());
    }

    [TestMethod]
    public void WithNoUsableOrigin_TheVolleyRowIsHidden_AndNothingIsSold()
    {
        var run = new SurvivalRun(Plan());
        run.Begin();

        run.LimitVolleyOrigins(Array.Empty<LdPoint>());

        Assert.AreEqual(FortResult.Hidden, run.TryBuyVolleyLevel());
        Assert.IsFalse(run.GetView().Fort.Volley.Available);
    }

    [TestMethod]
    public void TheShippedOffer_NowHoldsTheTwoVolleyLevels_AndTheOtherKindsAsBefore()
    {
        Assert.AreEqual(2, FortOffer.Offered.Volley);
        Assert.AreEqual(3, FortOffer.Offered.Gate);
        Assert.AreEqual(0, FortOffer.Offered.Towers, "the towers are Slice 7 and 8");
        Assert.IsTrue(FortOffer.Offered.FieldCare);
    }

    [TestMethod]
    public void AFortressWithNoOrigins_StillSellsNoVolley_WhateverIsOffered()
    {
        var plan = Plan() with { Volley = null };
        var run = new SurvivalRun(plan);
        run.Begin();

        Assert.AreEqual(FortResult.Hidden, run.TryBuyVolleyLevel());
    }
}
