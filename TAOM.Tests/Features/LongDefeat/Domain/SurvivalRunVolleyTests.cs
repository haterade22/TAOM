using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>
/// The volley side of the run (phase3-plan.md Slice 3, D30): the domain arms a shot and the director fires it, the cooldown starts only on a shot that fired, a withheld shot keeps the volley ready, the
/// towers' volleys run on their own clocks, a wave's tallies are written at its clear, and a run that ends inside a wave hands over what its open wave counted.
/// </summary>
[TestClass]
public class SurvivalRunVolleyTests
{
    private static readonly VolleySource Elven = VolleySource.Elven;
    private static readonly VolleySource Hornburg = VolleySource.Tower("hornburg");
    private static readonly VolleySource Wall = VolleySource.Tower("wall");

    private static readonly GatePlan Gate = new(200, new[] { new GateLevelPlan(50f, 200) });

    private static readonly TowerPlan Towers = new("fow_rohan_eastfold_bowman",
        new[] { new TowerLevelPlan(4, false, 250), new TowerLevelPlan(8, false, 300), new TowerLevelPlan(8, true, 400) },
        new TowerVolleyPlan("fow_tower_arrow", 6, 20f, 30f, 6f, 80f, 2.5f));

    private static readonly VolleyPlan Volley = new("ld_elven_arrow", 3f,
        new[] { new VolleyLevelPlan(350, 12, 45f, 8f, 90f, VolleyAimMode.Auto), new VolleyLevelPlan(300, 16, 40f, 7f, 85f, VolleyAimMode.Crosshair) });

    private static readonly FortPlan Everything = new(Gate, Towers, new CarePlan(0.75f, 150, 1), Volley);

    private static readonly FortOffer All = new(1, 3, true, 2);

    private static readonly LdPoint[] ElvenOrigins = { new(0f, 0f, 0f), new(10f, 0f, 0f) };

    private static MusterFrame Post(float x) => new(x, 0f, 0f, 0f, 1f);

    private static IReadOnlyList<LdTower> TwoTowers() => new[]
    {
        new LdTower("hornburg", Enumerable.Range(0, 8).Select(i => Post(i)).ToList(), new[] { new LdPoint(1f, 2f, 3f), new LdPoint(2f, 2f, 3f) }),
        new LdTower("wall", Enumerable.Range(0, 8).Select(i => Post(i)).ToList(), new[] { new LdPoint(4f, 5f, 6f) }),
        new LdTower("blind", Enumerable.Range(0, 8).Select(i => Post(i)).ToList(), Array.Empty<LdPoint>()),
        new LdTower("mute", Array.Empty<MusterFrame>(), new[] { new LdPoint(7f, 8f, 9f) }),
    };

    /// <summary>No quiet spell ends a wave here: the tests hold attackers on the field for as long as they like.</summary>
    private static RunRules Rules() => RunRules.Default with { PreparationSeconds = 1f, IntermissionSeconds = 10f, QuietSeconds = 1e6f, QuietAnySeconds = 1e6f, StrandedSeconds = 1e6f };

    private static RunPlan Plan(FortOffer? offered = null, int? stopAfter = null) =>
        new(
            "helms",
            new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30) },
            new WavePlan(new[] { new WaveSpec(20, new[] { new WaveTroop("orc", FormationSlot.Infantry, 3), new WaveTroop("skirm", FormationSlot.Ranged, 1) }) }, new WaveScaling(10, 300)),
            new ShopPlan(5000, new[] { new ShopOffer("militia", "Militia", "fow_militia", FormationSlot.Infantry, 12, 180) }, Everything),
            Rules(),
            new RunOptions { Seed = 1UL, StopAfterWaves = stopAfter })
        {
            Bounties = new Dictionary<string, int> { ["orc"] = 4, ["skirm"] = 6 },
            Offered = offered ?? All,
            Towers = TwoTowers(),
            Volley = new LdVolleyOrigins(ElvenOrigins),
        };

    private static RunSnapshot Fighting(int attackers = 10) => new(1f, attackers, 0, 40, 0, 0, 1000);

    private static SurvivalRun Begun(RunPlan? plan = null)
    {
        var run = new SurvivalRun(plan ?? Plan());
        run.Begin();
        return run;
    }

    /// <summary>A run that owns the Elven Volley (<paramref name="volley"/> levels) and the towers (<paramref name="towers"/> levels), bought in the preparation.</summary>
    private static SurvivalRun Owning(int volley = 1, int towers = 0, RunPlan? plan = null)
    {
        var run = Begun(plan);
        for (var i = 0; i < volley; i++)
            Assert.AreEqual(FortResult.Ok, run.TryBuyVolleyLevel());
        for (var i = 0; i < towers; i++)
            Assert.AreEqual(FortResult.Ok, run.TryBuyTowerLevel());
        return run;
    }

    /// <summary>Ticks until the first wave is running (the tick that hands out a purchase's effects does not start it) and returns the effects of every tick on the way.</summary>
    private static List<RunEffects> IntoTheWave(SurvivalRun run)
    {
        var seen = new List<RunEffects>();
        for (var i = 0; i < 20 && run.Phase != RunPhase.Wave; i++)
            seen.Add(run.Tick(Fighting()));
        Assert.AreEqual(RunPhase.Wave, run.Phase);
        if (run.WaveSeconds <= 0f)
            seen.Add(run.Tick(Fighting()));
        Assert.IsTrue(run.WaveSeconds > 0f, "the wave is running");
        return seen;
    }

    private static RunEffects Next(SurvivalRun run, int attackers = 10) => run.Tick(Fighting(attackers));

    private static void ClearTheWave(SurvivalRun run)
    {
        var before = run.WavesCleared;
        for (var i = 0; i < 500 && run.WavesCleared == before && run.Result == null; i++)
            run.Tick(Fighting(0));
        Assert.AreEqual(before + 1, run.WavesCleared, "the wave never cleared");
    }

    private static void IntoTheSecondWave(SurvivalRun run)
    {
        Assert.AreEqual(RunPhase.Intermission, run.Phase);
        run.TryStartWave();
        Next(run);
        Assert.AreEqual(RunPhase.Wave, run.Phase);
    }

    // ---- ArmVolley ----

    [TestMethod]
    public void ArmVolley_BeforeTheLevel_IsNotOwned_InAWaveAndBetween()
    {
        var run = Begun();
        Assert.AreEqual(VolleyArm.NotOwned, run.ArmVolley(), "the preparation, nothing bought");
        IntoTheWave(run);
        Assert.AreEqual(VolleyArm.NotOwned, run.ArmVolley(), "a wave, nothing bought");
        Assert.AreEqual(0, Next(run).FireVolleys.Count);
    }

    [TestMethod]
    public void ArmVolley_WithTheVolleyRowHidden_IsNotSold()
    {
        var hidden = new RunPlan("h", Plan().Garrison, Plan().Waves, Plan().Shop with { Fort = FortPlan.None }, Rules(), RunOptions.Default) { Offered = All };
        var run = Begun(hidden);
        IntoTheWave(run);

        Assert.AreEqual(VolleyArm.NotSold, run.ArmVolley());
    }

    [TestMethod]
    public void ArmVolley_BetweenWaves_IsNotInWave_EvenWithTheLevelOwned()
    {
        var run = Owning();

        Assert.AreEqual(VolleyArm.NotInWave, run.ArmVolley(), "the preparation");
        IntoTheWave(run);
        ClearTheWave(run);
        Assert.AreEqual(RunPhase.Intermission, run.Phase);
        Assert.AreEqual(VolleyArm.NotInWave, run.ArmVolley(), "an intermission");
    }

    [TestMethod]
    public void ArmVolley_AfterTheRunEnded_IsRunOver()
    {
        var run = Owning();
        IntoTheWave(run);
        run.Abandon();

        Assert.AreEqual(VolleyArm.RunOver, run.ArmVolley());
    }

    [TestMethod]
    public void ArmVolley_InAWave_Arms_AndTheNextTickHandsTheDirectorTheOrder()
    {
        var run = Owning();
        IntoTheWave(run);

        Assert.AreEqual(VolleyArm.Armed, run.ArmVolley());
        Assert.AreEqual(VolleyArm.Armed, run.ArmVolley(), "a second press before the tick is the same one shot");
        var fx = Next(run);

        var order = fx.FireVolleys.Single();
        Assert.AreEqual(Elven, order.Source);
        Assert.AreEqual("ld_elven_arrow", order.ItemId);
        Assert.AreEqual(12, order.Arrows);
        Assert.AreEqual(VolleyAimMode.Auto, order.Aim);
        Assert.AreEqual(8f, order.Radius);
        Assert.AreEqual(90f, order.Range);
        Assert.AreEqual(3f, order.OriginHeight);
        CollectionAssert.AreEqual(ElvenOrigins, order.Origins.ToArray());
        Assert.AreEqual(1, order.Wave);
        Assert.AreEqual(1, order.Use, "the first use of the wave");
        Assert.AreEqual(0, Next(run).FireVolleys.Count, "armed once, ordered once");
    }

    [TestMethod]
    public void TheOrder_FollowsTheLevelInForce()
    {
        var run = Owning(volley: 2);
        IntoTheWave(run);
        run.ArmVolley();

        var order = Next(run).FireVolleys.Single();

        Assert.AreEqual(16, order.Arrows);
        Assert.AreEqual(VolleyAimMode.Crosshair, order.Aim);
        Assert.AreEqual(7f, order.Radius);
        Assert.AreEqual(85f, order.Range);
    }

    [TestMethod]
    public void TheElvenVolley_IsReadyAtTheWaveStart_AndNothingIsOrderedUnasked()
    {
        var run = Owning();
        var ticks = IntoTheWave(run);

        Assert.IsTrue(run.GetView().Volleys.Ready, "no first delay: the player bought it to use it");
        Assert.IsTrue(ticks.All(fx => fx.FireVolleys.Count == 0), "the player or the bot fires it; it never looses itself");
        for (var i = 0; i < 5; i++)
            Assert.AreEqual(0, Next(run).FireVolleys.Count);
    }

    // ---- the cooldown (D30) ----

    [TestMethod]
    public void TheCooldown_StartsOnlyWhenTheShotFired()
    {
        var run = Owning();
        IntoTheWave(run);
        run.ArmVolley();
        Next(run);
        var firedAt = run.WaveSeconds;

        run.VolleyFired(Elven, 12);

        Assert.AreEqual(VolleyArm.Cooldown, run.ArmVolley());
        var waited = 0;
        while (run.ArmVolley() == VolleyArm.Cooldown && waited < 100)
        {
            Next(run);
            waited++;
        }

        Assert.AreEqual(45, waited, "the level's cooldown, from the tick the shot fired");
        Assert.AreEqual(45f, run.WaveSeconds - firedAt, 1e-3f);
    }

    [TestMethod]
    public void AWithheldShot_KeepsTheVolleyReady_AndAsksAgainAfterOneSecond()
    {
        var run = Owning();
        IntoTheWave(run);
        Assert.AreEqual(VolleyArm.Armed, run.ArmVolley());
        Assert.AreEqual(1, Next(run).FireVolleys.Count);

        run.VolleyWithheld(Elven, VolleyWithheldReason.NoTargets);

        Assert.IsFalse(run.GetView().Volleys.Ready, "not asked again within the second");
        Assert.AreEqual(VolleyArm.Cooldown, run.ArmVolley());
        Next(run);
        Assert.AreEqual(VolleyArm.Armed, run.ArmVolley(), "a second on, the full cooldown not spent");
        Assert.AreEqual(1, Next(run).FireVolleys.Count);
        Assert.AreEqual(VolleyWithheldReason.NoTargets, run.GetView().Volleys.Withheld, "the reason stays on the view for the HUD to age out");
    }

    [TestMethod]
    public void AShotThatFiresAfterAWithhold_StartsTheFullCooldown_AndTheNoteGoes()
    {
        var run = Owning();
        IntoTheWave(run);
        run.ArmVolley();
        Next(run);
        run.VolleyWithheld(Elven, VolleyWithheldReason.NoOwner);
        Assert.AreEqual(VolleyWithheldReason.NoOwner, run.GetView().Volleys.Withheld);
        Next(run);
        run.ArmVolley();
        Next(run);

        run.VolleyFired(Elven, 12);

        var view = run.GetView().Volleys;
        Assert.IsNull(view.Withheld, "a volley that fired has nothing left to explain");
        Assert.IsFalse(view.Ready);
        Assert.AreEqual(45f, view.SecondsLeft, 1e-3f);
    }

    [TestMethod]
    public void TheWithheldNote_ReadsHowLongAgoItWas_ForTheHudsThreeSeconds()
    {
        var run = Owning();
        IntoTheWave(run);
        run.ArmVolley();
        Next(run);

        run.VolleyWithheld(Elven, VolleyWithheldReason.NoTargets);
        Assert.AreEqual(0f, run.GetView().Volleys.WithheldSecondsAgo, 1e-3f);
        Next(run);
        Next(run);

        Assert.AreEqual(VolleyWithheldReason.NoTargets, run.GetView().Volleys.Withheld);
        Assert.AreEqual(2f, run.GetView().Volleys.WithheldSecondsAgo, 1e-3f);
    }

    // ---- the towers' volleys ----

    [TestMethod]
    public void TheTowerVolleys_FireOnTheirOwnClocks_FromTheFirstDelay_AndOnlyAtTheLastLevel()
    {
        var run = Owning(volley: 0, towers: 3);
        IntoTheWave(run);

        while (run.WaveSeconds < 19f)
            Assert.AreEqual(0, Next(run).FireVolleys.Count, "seconds " + run.WaveSeconds);
        var fx = Next(run);
        Assert.AreEqual(20f, run.WaveSeconds, 1e-3f);

        var orders = fx.FireVolleys.OrderBy(o => o.Source.Id).ToList();
        CollectionAssert.AreEqual(new[] { Hornburg, Wall }, orders.Select(o => o.Source).ToArray(), "a tower with no recorded volley spot has no clock, and one with no post has none (no archer could own its arrows)");
        var horn = orders[0];
        Assert.AreEqual("fow_tower_arrow", horn.ItemId);
        Assert.AreEqual(6, horn.Arrows);
        Assert.AreEqual(VolleyAimMode.Auto, horn.Aim);
        Assert.AreEqual(6f, horn.Radius);
        Assert.AreEqual(80f, horn.Range);
        Assert.AreEqual(2.5f, horn.OriginHeight);
        Assert.AreEqual(2, horn.Origins.Count);
        Assert.AreEqual(1, orders[1].Origins.Count);
        Assert.IsTrue(horn.Source.IsTower);
        Assert.AreEqual("hornburg", horn.Source.TowerId);
    }

    [TestMethod]
    public void TheTowerVolleys_AtLevelTwo_AreNotThere()
    {
        var run = Owning(volley: 0, towers: 2);
        IntoTheWave(run);

        for (var i = 0; i < 40; i++)
            Assert.AreEqual(0, Next(run).FireVolleys.Count);
        Assert.AreEqual(0, run.GetView().Volleys.Towers.Count);
    }

    [TestMethod]
    public void ATowerVolley_KeepsItsCooldownWhenWithheld_AndSpendsItOnlyWhenFired()
    {
        var run = Owning(volley: 0, towers: 3);
        IntoTheWave(run);
        while (run.WaveSeconds < 19f)
            Next(run);
        var fx = Next(run);
        Assert.AreEqual(2, fx.FireVolleys.Count, "setup: both towers are ordered at 20 s");
        run.VolleyFired(Hornburg, 6);
        run.VolleyWithheld(Wall, VolleyWithheldReason.NoTargets);

        var next = Next(run);
        Assert.IsFalse(next.FireVolleys.Any(o => o.Source == Hornburg), "fired: its 30 s have begun");
        Assert.IsTrue(next.FireVolleys.Any(o => o.Source == Wall), "withheld: a second on it asks again");

        var hornburgAgain = 0;
        for (var i = 0; i < 40 && hornburgAgain == 0; i++)
        {
            var more = Next(run);
            if (more.FireVolleys.Any(o => o.Source == Hornburg))
                hornburgAgain = i + 2;
        }

        Assert.AreEqual(30, hornburgAgain, "30 s from the tick it fired");
    }

    [TestMethod]
    public void ATowerVolley_WithNoOwner_StopsForTheWave_AndIsBackAtTheNext()
    {
        var run = Owning(volley: 0, towers: 3);
        IntoTheWave(run);
        while (run.WaveSeconds < 19f)
            Next(run);
        Assert.IsTrue(Next(run).FireVolleys.Any(o => o.Source == Wall), "setup: the wall is ordered at 20 s");
        run.VolleyWithheld(Wall, VolleyWithheldReason.NoOwner);

        for (var i = 0; i < 60; i++)
            Assert.IsFalse(Next(run).FireVolleys.Any(o => o.Source == Wall), "no post archer stands, and none comes back inside the wave");
        Assert.IsFalse(run.GetView().Volleys.Towers.Single(t => t.Source == Wall).Ready);
        Assert.IsTrue(Next(run).FireVolleys.Any(o => o.Source == Hornburg), "the other tower, never answered, is still ordered");

        ClearTheWave(run);
        Assert.AreEqual(new VolleyWaveRecord(0, 0, 0, 0, 1), run.Stats.Waves[0].Fort.TowerVolley, "the stop books the one attempt, not one a second");
        IntoTheSecondWave(run);
        while (run.WaveSeconds < 19f)
            Next(run);
        Assert.IsTrue(Next(run).FireVolleys.Any(o => o.Source == Wall), "the posts are manned again at the clear, so the next wave asks again");
    }

    [TestMethod]
    public void TheViewListsEachTowerSource_WithItsTimeLeft()
    {
        var run = Owning(volley: 0, towers: 3);
        IntoTheWave(run);

        var towers = run.GetView().Volleys.Towers.OrderBy(t => t.Source.Id).ToList();

        CollectionAssert.AreEqual(new[] { Hornburg, Wall }, towers.Select(t => t.Source).ToArray());
        Assert.IsTrue(towers.All(t => !t.Ready));
        Assert.IsTrue(towers.All(t => t.SecondsLeft > 17f && t.SecondsLeft < 20f), "20 s first delay less the wave seconds so far");
    }

    // ---- the clear ----

    [TestMethod]
    public void AtTheClear_EveryClockStops_AndAnArmedShotIsDropped()
    {
        var run = Owning(volley: 1, towers: 3);
        IntoTheWave(run);
        for (var i = 0; i < 3; i++)
            Next(run);
        Assert.AreEqual(VolleyArm.Armed, run.ArmVolley());

        var clearing = run.Tick(Fighting(0));

        Assert.AreEqual(1, run.WavesCleared, "the last attacker was gone, so this tick cleared the wave");
        Assert.AreEqual(0, clearing.FireVolleys.Count, "the shot armed for the clearing tick is dropped");
        Assert.AreEqual(VolleyArm.NotInWave, run.ArmVolley());
        for (var i = 0; i < 8; i++)
            Assert.AreEqual(0, Next(run).FireVolleys.Count, "an intermission orders nothing");
        Assert.IsFalse(run.GetView().Volleys.Ready);
        Assert.AreEqual(0, run.GetView().Volleys.Towers.Count);
    }

    [TestMethod]
    public void AnArmedShot_ThatNeverWasOrdered_DoesNotLeakIntoTheNextWave()
    {
        var run = Owning();
        IntoTheWave(run);
        for (var i = 0; i < 3; i++)
            Next(run);
        run.ArmVolley();
        run.Tick(Fighting(0));
        Assert.AreEqual(1, run.WavesCleared);

        IntoTheSecondWave(run);

        Assert.AreEqual(0, Next(run).FireVolleys.Count, "nobody armed the second wave's volley");
    }

    [TestMethod]
    public void AShotReportedAfterTheClear_IsNotCountedInTheNextWave()
    {
        var run = Owning();
        IntoTheWave(run);
        ClearTheWave(run);
        run.VolleyFired(Elven, 12);
        run.VolleyWithheld(Elven, VolleyWithheldReason.NoTargets);

        IntoTheSecondWave(run);
        ClearTheWave(run);

        Assert.AreEqual(VolleyWaveRecord.None, run.Stats.Waves[1].Fort.Volley);
    }

    [TestMethod]
    public void TheClocksStartAgainEachWave_TheCooldownDoesNotCarry()
    {
        var run = Owning();
        IntoTheWave(run);
        run.ArmVolley();
        Next(run);
        run.VolleyFired(Elven, 12);
        ClearTheWave(run);

        IntoTheSecondWave(run);

        Assert.AreEqual(VolleyArm.Armed, run.ArmVolley(), "ready at the new wave's start though it fired late in the last");
        Assert.AreEqual(2, Next(run).FireVolleys.Single().Wave);
    }

    // ---- the tallies ----

    [TestMethod]
    public void AWavesRow_CarriesItsOwnTallies_ElvenAndTowerApart()
    {
        var run = Owning(volley: 1, towers: 3);
        IntoTheWave(run);
        run.ArmVolley();
        Next(run);
        run.VolleyFired(Elven, 12);
        run.VolleyWithheld(Elven, VolleyWithheldReason.NoTargets);
        run.VolleyFired(Hornburg, 6);
        run.VolleyFired(Wall, 4);
        run.VolleyWithheld(Wall, VolleyWithheldReason.NoOwner);
        run.VolleyWithheld(Wall, VolleyWithheldReason.NoOwner);

        ClearTheWave(run);

        var fort = run.Stats.Waves[0].Fort;
        Assert.AreEqual(new VolleyWaveRecord(1, 12, 0, 0, 1), fort.Volley);
        Assert.AreEqual(new VolleyWaveRecord(2, 10, 0, 0, 2), fort.TowerVolley);
    }

    [TestMethod]
    public void ASecondWaveStartsItsTalliesFromZero()
    {
        var run = Owning();
        IntoTheWave(run);
        run.ArmVolley();
        Next(run);
        run.VolleyFired(Elven, 12);
        ClearTheWave(run);
        IntoTheSecondWave(run);
        run.ArmVolley();
        Next(run);
        run.VolleyFired(Elven, 12);
        run.VolleyFired(Elven, 12);

        ClearTheWave(run);

        Assert.AreEqual(1, run.Stats.Waves[0].Fort.Volley.Uses);
        Assert.AreEqual(new VolleyWaveRecord(2, 24, 0, 0, 0), run.Stats.Waves[1].Fort.Volley);
    }

    [TestMethod]
    public void TheUseNumber_CountsTheShotsThatFired_NotTheOnesAskedFor()
    {
        var run = Owning();
        IntoTheWave(run);
        run.ArmVolley();
        Assert.AreEqual(1, Next(run).FireVolleys.Single().Use);
        run.VolleyWithheld(Elven, VolleyWithheldReason.NoTargets);
        Next(run);
        run.ArmVolley();
        Assert.AreEqual(1, Next(run).FireVolleys.Single().Use, "withheld, so the same first use is asked again");
        run.VolleyFired(Elven, 12);
        while (run.ArmVolley() != VolleyArm.Armed)
            Next(run);

        Assert.AreEqual(2, Next(run).FireVolleys.Single().Use);
    }

    [TestMethod]
    public void ReportsOfAnUnknownSource_OrABadArrowCount_ChangeNothing()
    {
        var run = Owning();
        IntoTheWave(run);

        run.VolleyFired(VolleySource.Tower("nowhere"), 6);
        run.VolleyWithheld(VolleySource.Tower("nowhere"), VolleyWithheldReason.NoOwner);
        run.VolleyWithheld(Elven, VolleyWithheldReason.NoTargets);
        run.VolleyFired(Elven, -5);
        ClearTheWave(run);

        Assert.AreEqual(new VolleyWaveRecord(1, 0, 0, 0, 1), run.Stats.Waves[0].Fort.Volley, "the elven report counted, the unknown tower's did not, and arrows never go negative");
        Assert.AreEqual(VolleyWaveRecord.None, run.Stats.Waves[0].Fort.TowerVolley);
    }

    // ---- the end of a run ----

    [TestMethod]
    public void ARunThatEndsInAWave_HandsOverTheOpenWavesTallies_AndCountsNothingMore()
    {
        var run = Owning(volley: 1, towers: 3);
        IntoTheWave(run);
        run.ArmVolley();
        Next(run);
        run.VolleyFired(Elven, 12);
        run.VolleyFired(Hornburg, 6);
        run.VolleyWithheld(Wall, VolleyWithheldReason.NoOwner);
        Assert.IsNull(run.VolleyAtEnd, "nothing before the run ends");

        run.Abandon();

        var flush = run.VolleyAtEnd!;
        Assert.AreEqual(1, flush.Wave);
        Assert.AreEqual(new VolleyWaveRecord(1, 12, 0, 0, 0), flush.Elven);
        Assert.AreEqual(new VolleyWaveRecord(1, 6, 0, 0, 1), flush.Tower);
        Assert.AreEqual(0, run.Stats.Waves.Count, "the report keeps no row for the wave a run ends in (Slice 13's decision)");

        run.VolleyFired(Elven, 12);
        run.VolleyWithheld(Elven, VolleyWithheldReason.NoTargets);

        Assert.AreEqual(new VolleyWaveRecord(1, 12, 0, 0, 0), run.VolleyAtEnd!.Elven, "a report after the end counts for nothing");
        Assert.AreEqual(0, Next(run).FireVolleys.Count);
        Assert.IsFalse(run.GetView().Volleys.Ready);
    }

    [TestMethod]
    public void ARunThatEndsInAWave_WithNothingFired_StillHandsOverTheZeroRow()
    {
        var run = Owning();
        IntoTheWave(run);

        run.Abandon();

        Assert.AreEqual(VolleyWaveRecord.None, run.VolleyAtEnd!.Elven);
    }

    [TestMethod]
    public void ARunThatEndsAtItsClear_OrBetweenWaves_HasNoOpenWaveToHandOver()
    {
        var atClear = Owning(plan: Plan(stopAfter: 1));
        IntoTheWave(atClear);
        atClear.ArmVolley();
        Next(atClear);
        atClear.VolleyFired(Elven, 12);
        ClearTheWave(atClear);
        Assert.AreEqual(RunEndReason.TargetReached, atClear.Result!.Reason);
        Assert.IsNull(atClear.VolleyAtEnd, "the wave was closed into the report's row, once");
        Assert.AreEqual(1, atClear.Stats.Waves[0].Fort.Volley.Uses);

        var between = Owning();
        between.Abandon();
        Assert.IsNull(between.VolleyAtEnd);

        var inIntermission = Owning();
        IntoTheWave(inIntermission);
        ClearTheWave(inIntermission);
        inIntermission.Abandon();
        Assert.IsNull(inIntermission.VolleyAtEnd);
    }

    // ---- the view ----

    [TestMethod]
    public void TheView_ShowsTheElvenVolleyOwnedFromThePurchase_AndReadyOnlyInAWave()
    {
        var run = Begun();
        Assert.IsFalse(run.GetView().Volleys.Owned);

        run.TryBuyVolleyLevel();
        var prep = run.GetView().Volleys;
        Assert.IsTrue(prep.Owned);
        Assert.IsFalse(prep.Ready, "the preparation is no wave");

        IntoTheWave(run);
        Assert.IsTrue(run.GetView().Volleys.Ready);
        run.ArmVolley();
        Assert.IsFalse(run.GetView().Volleys.Ready, "an armed shot is not ready again until it is fired or withheld");
    }

    [TestMethod]
    public void TheViewTime_RunsDownFromTheCooldown()
    {
        var run = Owning();
        IntoTheWave(run);
        run.ArmVolley();
        Next(run);
        run.VolleyFired(Elven, 12);
        for (var i = 0; i < 10; i++)
            Next(run);

        Assert.AreEqual(35f, run.GetView().Volleys.SecondsLeft, 1e-3f);
    }

    // ---- shipped play ----

    [TestMethod]
    public void WithNothingOffered_NothingIsEverOwnedOrOrdered_ThroughAWholeRun()
    {
        var run = Begun(Plan(FortOffer.None));
        Assert.AreEqual(FortResult.NotOffered, run.TryBuyVolleyLevel());
        Assert.AreEqual(FortResult.NotOffered, run.TryBuyTowerLevel());

        for (var wave = 0; wave < 2; wave++)
        {
            for (var i = 0; i < 20 && run.Phase != RunPhase.Wave; i++)
                Assert.AreEqual(0, run.Tick(Fighting()).FireVolleys.Count);
            for (var i = 0; i < 60; i++)
            {
                Assert.AreEqual(VolleyArm.NotSold, run.ArmVolley());
                var fx = Next(run);
                Assert.AreEqual(0, fx.FireVolleys.Count);
                Assert.IsFalse(run.GetView().Volleys.Ready);
            }

            ClearTheWave(run);
            run.TryStartWave();
        }

        Assert.AreEqual(VolleyWaveRecord.None, run.Stats.Waves[0].Fort.Volley);
        Assert.AreEqual(VolleyWaveRecord.None, run.Stats.Waves[0].Fort.TowerVolley);
        Assert.IsNull(run.VolleyAtEnd);
    }
}
