using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>
/// Phase 3 Slice 8, tower level 3 in the run (phase3-plan.md D20, D31): the build offers the level that adds the tower volley, and it is sold only when some usable tower records a volley spot, so a player
/// never pays for a volley that has no clock. With none the row stops at level 2 and answers <c>NotOffered</c>; at level 3 the buy answers <c>MaxLevel</c>, which is the wording the panel shows then.
/// </summary>
[TestClass]
public class SurvivalRunTowerVolleyTests
{
    private static readonly GatePlan Gate = new(200, new[] { new GateLevelPlan(50f, 200) });

    private static readonly TowerPlan Towers = new("fow_rohan_eastfold_bowman",
        new[] { new TowerLevelPlan(4, false, 250), new TowerLevelPlan(8, false, 300), new TowerLevelPlan(8, true, 400) },
        new TowerVolleyPlan("fow_tower_arrow", 6, 20f, 30f, 6f, 80f, 2.5f));

    private static MusterFrame Post(float x) => new(x, 0f, 0f, 0f, 1f);

    private static IReadOnlyList<LdTower> Towered(bool withSpots) => new[]
    {
        new LdTower("hornburg", Enumerable.Range(0, 8).Select(i => Post(i)).ToList(), withSpots ? new[] { new LdPoint(1f, 2f, 3f) } : Array.Empty<LdPoint>()),
        new LdTower("wall", Enumerable.Range(0, 8).Select(i => Post(i)).ToList(), Array.Empty<LdPoint>()),
    };

    private static RunRules Rules() => RunRules.Default with { PreparationSeconds = 1f, IntermissionSeconds = 10f, QuietSeconds = 1e6f, QuietAnySeconds = 1e6f, StrandedSeconds = 1e6f };

    private static RunPlan Plan(bool withSpots, TowerPlan? towers = null, FortOffer? offered = null) =>
        new(
            "helms",
            new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30) },
            new WavePlan(new[] { new WaveSpec(20, new[] { new WaveTroop("orc", FormationSlot.Infantry, 3), new WaveTroop("skirm", FormationSlot.Ranged, 1) }) }, new WaveScaling(10, 300)),
            new ShopPlan(9000, new[] { new ShopOffer("militia", "Militia", "fow_militia", FormationSlot.Infantry, 12, 180) }, new FortPlan(Gate, towers ?? Towers, null, null)),
            Rules(),
            new RunOptions { Seed = 1UL })
        {
            Bounties = new Dictionary<string, int> { ["orc"] = 4, ["skirm"] = 6 },
            Offered = offered ?? FortOffer.Offered,
            Towers = Towered(withSpots),
        };

    private static SurvivalRun Begun(bool withSpots, TowerPlan? towers = null, FortOffer? offered = null)
    {
        var run = new SurvivalRun(Plan(withSpots, towers, offered));
        run.Begin();
        return run;
    }

    [TestMethod]
    public void TheBuild_OffersTheThirdTowerLevel()
    {
        Assert.AreEqual(3, FortOffer.Offered.Towers, "tower level 3, the tower volley, landed in Slice 8");
    }

    [TestMethod]
    public void WithARecordedVolleySpot_TheThreeLevelsAreSold_AndTheThirdPaysItsPrice()
    {
        var run = Begun(true);

        Assert.AreEqual(FortResult.Ok, run.TryBuyTowerLevel());
        Assert.AreEqual(FortResult.Ok, run.TryBuyTowerLevel());
        var view = run.GetView().Fort.Towers;
        Assert.IsTrue(view.Available, "level 3 is for sale once a tower records a spot");
        Assert.AreEqual((2, 3, 400), (view.Level, view.MaxLevel, view.Price));

        Assert.AreEqual(FortResult.Ok, run.TryBuyTowerLevel());
        Assert.AreEqual(3, run.TowerLevel);
        Assert.AreEqual(FortResult.MaxLevel, run.TryBuyTowerLevel(), "the top level answers MaxLevel, the towers' own wording in the panel");
    }

    [TestMethod]
    public void WithNoVolleySpotAnywhere_LevelThreeIsNotSold_AndNobodyPaysForIt()
    {
        var run = Begun(false);
        run.TryBuyTowerLevel();
        run.TryBuyTowerLevel();
        var gold = run.Purse.Balance;

        Assert.AreEqual(FortResult.NotOffered, run.TryBuyTowerLevel());
        Assert.AreEqual(gold, run.Purse.Balance, "refused with no gold taken");
        Assert.AreEqual(2, run.TowerLevel);
        var row = run.GetView().Fort.Towers;
        Assert.IsFalse(row.Available);
        Assert.AreEqual(2, row.MaxLevel, "the row stops where the code can deliver");
        Assert.IsNull(run.GetView().Fort.TowerVolleyShown);
    }

    [TestMethod]
    public void SpotsOnlyOnATowerWithNoPosts_LevelThreeIsNotSold_BecauseNoArcherCouldOwnItsArrows()
    {
        var run = new SurvivalRun(Plan(true) with
        {
            Towers = new[]
            {
                new LdTower("manned", Enumerable.Range(0, 8).Select(i => Post(i)).ToList(), Array.Empty<LdPoint>()),
                new LdTower("mute", Array.Empty<MusterFrame>(), new[] { new LdPoint(1f, 2f, 3f) }),
            },
        });
        run.Begin();
        run.TryBuyTowerLevel();
        run.TryBuyTowerLevel();
        var gold = run.Purse.Balance;

        Assert.AreEqual(FortResult.NotOffered, run.TryBuyTowerLevel());
        Assert.AreEqual(gold, run.Purse.Balance);
        Assert.AreEqual(2, run.GetView().Fort.Towers.MaxLevel);
    }

    [TestMethod]
    public void ALevelThatAddsNeitherPostsNorAWorkingVolley_IsNotSold()
    {
        var dropped = new TowerPlan(Towers.Troop, new[] { new TowerLevelPlan(4, false, 250), new TowerLevelPlan(8, false, 300), new TowerLevelPlan(8, false, 400) }, null);
        var run = Begun(true, dropped);
        run.TryBuyTowerLevel();
        run.TryBuyTowerLevel();
        var gold = run.Purse.Balance;

        Assert.AreEqual(FortResult.NotOffered, run.TryBuyTowerLevel(), "the reader clears the flag when the volley block drops, which leaves a level that adds nothing");
        Assert.AreEqual(gold, run.Purse.Balance);
        Assert.AreEqual(2, run.GetView().Fort.Towers.MaxLevel);
    }

    [TestMethod]
    public void ASpotTheBootCheckDropped_TakesLevelThreeOffSale()
    {
        var run = Begun(true);
        run.LimitTowers(Towered(false));
        run.TryBuyTowerLevel();
        run.TryBuyTowerLevel();

        Assert.AreEqual(FortResult.NotOffered, run.TryBuyTowerLevel());
    }

    [TestMethod]
    public void ALevelThatClaimsAVolleyWithNoVolleyBlock_IsNotSold()
    {
        var bare = new TowerPlan(Towers.Troop, Towers.Levels, null);
        var run = Begun(true, bare);
        run.TryBuyTowerLevel();
        run.TryBuyTowerLevel();

        Assert.AreEqual(FortResult.NotOffered, run.TryBuyTowerLevel());
    }

    [TestMethod]
    public void WhenTheBuildOffersTwoLevels_LevelThreeStaysNotOffered_EvenWithSpots()
    {
        var run = Begun(true, offered: new FortOffer(3, 2, true, 2));
        run.TryBuyTowerLevel();
        run.TryBuyTowerLevel();

        Assert.AreEqual(FortResult.NotOffered, run.TryBuyTowerLevel());
        Assert.AreEqual(2, run.GetView().Fort.Towers.MaxLevel);
    }

    [TestMethod]
    public void TheRow_DescribesTheVolleyOnTheLevelThatAddsIt_AndOnlyThere()
    {
        var run = Begun(true);

        Assert.IsNull(run.GetView().Fort.TowerVolleyShown, "level 1 adds posts only");
        run.TryBuyTowerLevel();
        Assert.IsNull(run.GetView().Fort.TowerVolleyShown, "level 2 adds posts only");
        run.TryBuyTowerLevel();
        Assert.AreEqual(Towers.Volley, run.GetView().Fort.TowerVolleyShown, "the next buy, level 3, adds the volley: the row names it");
        run.TryBuyTowerLevel();
        Assert.AreEqual(Towers.Volley, run.GetView().Fort.TowerVolleyShown, "owned, the top level is still the one described");
    }

    [TestMethod]
    public void LevelThree_AddsNoPosts_SoTheGarrisonCapNeverHoldsItBack()
    {
        var run = Begun(true);
        run.TryBuyTowerLevel();
        run.TryBuyTowerLevel();

        Assert.IsFalse(run.GetView().Fort.Towers.Full, "no posts to fit, so never listed as held back by the cap");
        Assert.AreEqual(FortResult.Ok, run.TryBuyTowerLevel());
    }
}
