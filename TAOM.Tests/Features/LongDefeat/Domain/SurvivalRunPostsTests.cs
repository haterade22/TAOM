using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>
/// Phase 3 Slice 7, the posted archers in the run (phase3-plan.md D20): the tower rows are sold only against the towers that passed the boot check, a fill's spawned and short posts and the standing archers
/// reach the wave's report row, and a clear's refill is told before any pack and carries the health boons' factor.
/// </summary>
[TestClass]
public class SurvivalRunPostsTests
{
    private static readonly TowerPlan Towers = new("fow_rohan_eastfold_bowman",
        new[] { new TowerLevelPlan(4, false, 250), new TowerLevelPlan(8, false, 300), new TowerLevelPlan(8, true, 400) },
        new TowerVolleyPlan("fow_tower_arrow", 6, 20f, 30f, 6f, 80f, 2.5f));

    private static MusterFrame Post(float x) => new(x, 0f, 0f, 0f, 1f);

    private static IReadOnlyList<LdTower> TwoTowers(int postsEach = 8) => new[]
    {
        new LdTower("hornburg", Enumerable.Range(0, postsEach).Select(i => Post(i)).ToList(), Array.Empty<LdPoint>()),
        new LdTower("wall", Enumerable.Range(0, postsEach).Select(i => Post(i)).ToList(), Array.Empty<LdPoint>()),
    };

    private static RunPlan Plan(IReadOnlyList<LdTower>? towers = null, BoonPlan? boons = null, int? stopAfter = null, RunRules? rules = null) =>
        new(
            "helms",
            new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30) },
            new WavePlan(new[] { new WaveSpec(20, new[] { new WaveTroop("orc", FormationSlot.Infantry, 3), new WaveTroop("skirm", FormationSlot.Ranged, 1) }) }, new WaveScaling(10, 300)),
            new ShopPlan(5000, new[] { new ShopOffer("militia", "Militia", "fow_militia", FormationSlot.Infantry, 12, 180) }, new FortPlan(null, Towers, null, null)),
            rules ?? RunRules.Default with { PreparationSeconds = 1f, IntermissionSeconds = 10f },
            new RunOptions { Seed = 1UL, StopAfterWaves = stopAfter })
        {
            Offered = FortOffer.Offered,
            Towers = towers ?? TwoTowers(),
            Boons = boons ?? BoonPlan.None,
        };

    private static RunSnapshot Snap(int defenders = 40, int attackers = 0) => new(1f, attackers, 0, defenders, 0, 0, 1000) { Gates = Array.Empty<LdGateFacts>() };

    private static SurvivalRun Begun(RunPlan plan)
    {
        var run = new SurvivalRun(plan);
        run.Begin();
        return run;
    }

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

    // ---- what is for sale (D31, A23, the Points rule) ----

    [TestMethod]
    public void TheTowerRow_IsSoldAgainstTheRecordedTowers_UntilTheBootCheckTakesThemAway()
    {
        var run = Begun(Plan());
        Assert.IsTrue(run.GetView().Fort.Towers.Available);

        run.LimitTowers(Array.Empty<LdTower>());

        Assert.IsFalse(run.GetView().Fort.Towers.Available);
        Assert.AreEqual(FortResult.Hidden, run.TryBuyTowerLevel());
        Assert.AreEqual(5000, run.Gold, "nothing was spent");
    }

    [TestMethod]
    public void ATowerWithOnlyVolleySpots_SellsNoPostsLevel()
    {
        var spotsOnly = new[] { new LdTower("hornburg", Array.Empty<MusterFrame>(), new[] { new LdPoint(1f, 2f, 3f) }) };
        var run = Begun(Plan(spotsOnly));

        Assert.IsFalse(run.GetView().Fort.Towers.Available);
        Assert.AreEqual(FortResult.Hidden, run.TryBuyTowerLevel(), "nobody pays for posts that cannot be manned");
        Assert.AreEqual(5000, run.Gold);
    }

    [TestMethod]
    public void AUsableSubset_IsWhatTheLevelAddsPostsFor()
    {
        var run = Begun(Plan());
        run.LimitTowers(new[] { TwoTowers(2)[0] });

        Assert.AreEqual(FortResult.Ok, run.TryBuyTowerLevel());
        var fx = run.Tick(Snap(30));

        Assert.AreEqual(4, fx.FillPosts!.Posts, "the level asks for 4 posts a tower; the kept tower records 2 and fills those, the fill carries the level's number");
        Assert.AreEqual(2, run.Towers.Single().Posts.Count);
        Assert.AreEqual("hornburg", run.Towers.Single().Id);
    }

    [TestMethod]
    public void TheTowerRow_ShowsTheTopOfWhatIsOffered_NotTheShopsThirdLevel()
    {
        var run = Begun(Plan());

        var row = run.GetView().Fort.Towers;
        Assert.AreEqual(2, row.MaxLevel, "level 3 is the tower volley, which this build does not deliver");

        run.TryBuyTowerLevel();
        run.TryBuyTowerLevel();
        Assert.AreEqual(FortResult.NotOffered, run.TryBuyTowerLevel());
        Assert.AreEqual((2, 2, false), (run.GetView().Fort.Towers.Level, run.GetView().Fort.Towers.MaxLevel, run.GetView().Fort.Towers.Available));
    }

    [TestMethod]
    public void TheTowerRow_StaysListedAtLevelZero_WhenTheGarrisonIsFull_ButCannotBeBought()
    {
        // 8 posts do not fit under the cap with 215 of 220 standing; as soldiers die the posts fit again. The row must not vanish and return with them.
        var run = Begun(Plan(rules: RunRules.Default with { PreparationSeconds = 50f, GarrisonCap = 220 }));
        run.Tick(Snap(215));

        var full = run.GetView().Fort.Towers;
        Assert.IsFalse(full.Available, "the buy would be refused");
        Assert.IsTrue(full.Full, "but the row is still for sale, only the cap holds it back");

        run.Tick(Snap(150));
        var room = run.GetView().Fort.Towers;
        Assert.IsTrue(room.Available);
        Assert.IsFalse(room.Full);
    }

    [TestMethod]
    public void ATowerRowThatIsNotOffered_OrHidden_IsNeverFull()
    {
        var run = Begun(Plan(rules: RunRules.Default with { PreparationSeconds = 50f, GarrisonCap = 220 }));
        run.Tick(Snap(215));
        run.LimitTowers(Array.Empty<LdTower>());

        Assert.IsFalse(run.GetView().Fort.Towers.Full, "a hidden row has no cap to wait for");
    }

    // ---- the fill ----

    [TestMethod]
    public void AClearsRefill_IsToldWithTheTowerTroopTheLevelsPostsAndTheRoomLeft_BeforeAnyPack()
    {
        var run = Begun(Plan());
        run.TryBuyTowerLevel();
        run.TryBuyTowerLevel();
        run.Tick(Snap(30));

        var fx = ClearTo(run, 1, () => Snap(60));

        Assert.IsNotNull(fx.FillPosts);
        Assert.AreEqual("fow_rohan_eastfold_bowman", fx.FillPosts!.Troop);
        Assert.AreEqual(8, fx.FillPosts.Posts);
        Assert.AreEqual(1f, fx.FillPosts.HealthFactor);
        Assert.AreEqual(0, fx.DefenderBatches.Count(b => b.Reinforcement));
    }

    [TestMethod]
    public void TheFill_CarriesTheHealthBoonsFactorOfTheTowerTroopsGroup()
    {
        // The tower troop is a ranged soldier of the garrison's side: a pack of him would carry the archers' health factor, so the posts do too.
        var boon = new BoonRow("bowyers", BoonCategory.WarChest, "{=K_b}b", "{=T_b}t", new[] { new BoonEffect("healthPercent", 20f, BoonTarget.Archers) });
        var run = Begun(Plan(boons: new BoonPlan(new[] { boon }, 1)));
        run.TryBuyTowerLevel();
        run.Tick(Snap(30));
        ClearTo(run, 3);
        Assert.AreEqual(PickResult.Ok, run.TryPickBoon("bowyers"));

        run.TryBuyTowerLevel();
        var fill = run.Tick(Snap(40)).FillPosts!;

        Assert.AreEqual(1.2f, fill.HealthFactor, 0.0001f);
    }

    [TestMethod]
    public void AClear_AfterTheRowsWereTakenOffSale_RefillsNothing()
    {
        var run = Begun(Plan());
        run.TryBuyTowerLevel();
        run.Tick(Snap(30));
        run.LimitTowers(Array.Empty<LdTower>());

        var fx = ClearTo(run, 1);

        Assert.IsNull(fx.FillPosts, "no tower has a post to man, so the clear asks the field for nothing");
    }

    // ---- the wave row ----

    [TestMethod]
    public void AWavesRow_SaysHowManyPostsWereFilledLeftShortMannedAndLost()
    {
        var run = Begun(Plan());
        run.TryBuyTowerLevel();
        run.Tick(Snap(30));
        run.PostsFilled(new LdPostsFilled(new[] { new LdTowerFill("hornburg", 4, 0, 4, 0, 0), new LdTowerFill("wall", 4, 0, 2, 2, 0) }, 0));

        // The wave starts on the next tick: eight archers stand then, six by the time it clears.
        run.NotePosts(6);
        run.Tick(Snap(40, 5));
        run.NotePosts(6);
        ClearTo(run, 1, () => Snap(40));

        var posts = run.Stats.Waves.Single().Fort.Posts;
        Assert.AreEqual(new PostsWaveRecord(6, 2, 6, 0), posts, "filled is the 4 + 2 spawned, short the 2 the cap left empty, manned the archers standing as the wave began");
    }

    [TestMethod]
    public void AnArcherWhoDiesInTheWave_IsCountedLost()
    {
        var run = Begun(Plan());
        run.TryBuyTowerLevel();
        run.Tick(Snap(30));
        run.PostsFilled(new LdPostsFilled(new[] { new LdTowerFill("hornburg", 4, 0, 4, 0, 0) }, 0));
        var snap = new Func<RunSnapshot>(() => Snap(40));
        for (var i = 0; i < 3; i++)
        {
            run.NotePosts(4);
            run.Tick(Snap(40, 5));
        }

        run.NotePosts(1);
        for (var i = 0; run.WavesCleared < 1 && i < 5000; i++)
        {
            run.NotePosts(1);
            run.Tick(snap());
        }

        Assert.AreEqual(3, run.Stats.Waves.Single().Fort.Posts.Lost);
        Assert.AreEqual(4, run.Stats.Waves.Single().Fort.Posts.Manned);
    }

    [TestMethod]
    public void ARunWithNoPosts_WritesAnEmptyPostsRow()
    {
        var run = Begun(Plan());
        ClearTo(run, 1);

        Assert.AreEqual(PostsWaveRecord.None, run.Stats.Waves.Single().Fort.Posts);
    }

    [TestMethod]
    public void TheFilledCount_IsPerWave_NotCumulative()
    {
        var run = Begun(Plan());
        run.TryBuyTowerLevel();
        run.Tick(Snap(30));
        run.PostsFilled(new LdPostsFilled(new[] { new LdTowerFill("hornburg", 4, 0, 4, 0, 0) }, 0));
        run.NotePosts(4);
        ClearTo(run, 1, () => Snap(40));

        run.NotePosts(4);
        run.PostsFilled(new LdPostsFilled(new[] { new LdTowerFill("hornburg", 4, 3, 1, 0, 0) }, 0));
        ClearTo(run, 2, () => Snap(40));

        Assert.AreEqual(4, run.Stats.Waves[0].Fort.Posts.Filled);
        Assert.AreEqual(1, run.Stats.Waves[1].Fort.Posts.Filled);
    }
}
