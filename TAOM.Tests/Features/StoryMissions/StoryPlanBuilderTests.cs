using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Features.StoryMissions.Domain;

namespace TAOM.Tests.Features.StoryMissions;

/// <summary>A story mission is a Long Defeat run with its shop, affixes, boons, classes and fortifications all off, ending after the authored waves.</summary>
[TestClass]
public class StoryPlanBuilderTests
{
    [TestMethod]
    public void Build_CarriesTheDefinitionsGarrisonWavesAndRules()
    {
        var rules = RunRules.Default with { PreparationSeconds = 20f };
        var definition = StoryTestData.Definition(rules: rules);

        var plan = StoryPlanBuilder.Build(definition, RunOptions.Default);

        Assert.AreEqual("m1", plan.FortressId);
        Assert.AreSame(definition.Garrison, plan.Garrison);
        Assert.AreSame(definition.Waves, plan.Waves);
        Assert.AreEqual(20f, plan.Rules.PreparationSeconds);
    }

    [TestMethod]
    public void Build_CarriesTheRecordedKeepAndMusterPoints()
    {
        var keep = new KeepPoint(1f, 2f, 3f, 12f);
        MusterFrame.TryCreate(1f, 2f, 3f, 0.6f, 0.8f, out var frame);
        var muster = new MusterPoints(frame, null);

        var plan = StoryPlanBuilder.Build(StoryTestData.Definition(keep: keep, muster: muster), RunOptions.Default);

        Assert.AreEqual(keep, plan.Keep);
        Assert.AreSame(muster, plan.Muster);
    }

    [TestMethod]
    public void Build_StopsAfterTheAuthoredWaves_SoNoEndlessScalingApplies()
    {
        Assert.AreEqual(1, StoryPlanBuilder.Build(StoryTestData.Definition(waves: 1), RunOptions.Default).Options.StopAfterWaves);
        Assert.AreEqual(3, StoryPlanBuilder.Build(StoryTestData.Definition(waves: 3), RunOptions.Default).Options.StopAfterWaves);
    }

    [TestMethod]
    public void Build_OverridesAWaveTargetTheCallerPassed()
    {
        var plan = StoryPlanBuilder.Build(StoryTestData.Definition(waves: 2), RunOptions.Default with { StopAfterWaves = 9 });

        Assert.AreEqual(2, plan.Options.StopAfterWaves);
    }

    [TestMethod]
    public void Build_KeepsTheCallersAutoplay_ButAsksForNoClass()
    {
        var plan = StoryPlanBuilder.Build(StoryTestData.Definition(), RunOptions.Default with { Autoplay = true, ClassId = "ranger" });

        Assert.IsTrue(plan.Options.Autoplay);
        Assert.IsNull(plan.Options.ClassId);
        Assert.IsNull(plan.Loadout);
        Assert.AreEqual("", plan.ClassName);
    }

    [TestMethod]
    public void Build_SellsNothing_AndStartsWithNoGold()
    {
        var plan = StoryPlanBuilder.Build(StoryTestData.Definition(), RunOptions.Default);

        Assert.AreEqual(0, plan.Shop.StartingGold);
        Assert.AreEqual(0, plan.Shop.Offers.Count);
        Assert.IsNull(plan.Shop.Fort);
        Assert.IsNull(plan.Shop.Locations);
        Assert.AreEqual(0, plan.Bounties.Count);
    }

    [TestMethod]
    public void Build_TurnsOffAffixesBoonsFortificationsAndPlaces()
    {
        var plan = StoryPlanBuilder.Build(StoryTestData.Definition(), RunOptions.Default);

        Assert.AreSame(AffixPlan.None, plan.Affixes);
        Assert.AreSame(BoonPlan.None, plan.Boons);
        Assert.AreEqual(FortOffer.None, plan.Offered);
        Assert.AreEqual(LocationOffer.None, plan.Locations);
        Assert.AreEqual(0, plan.Towers.Count);
        Assert.IsNull(plan.Volley);
        Assert.IsNull(plan.Sally);
    }

    [TestMethod]
    public void Build_APlanThatRuns_ShowsNoShopOffersAndNoBoonOffer()
    {
        var run = new SurvivalRun(StoryPlanBuilder.Build(StoryTestData.Definition(), RunOptions.Default));
        run.Begin();

        var view = run.GetView();

        Assert.AreEqual(0, view.Offers.Count);
        Assert.AreEqual(0, view.BoonOffer.Count);
        Assert.AreEqual(0, view.Gold);
    }

    [TestMethod]
    public void Build_TheTroopsToLoad_AreTheGarrisonAndTheWavesOnly()
    {
        var plan = StoryPlanBuilder.Build(StoryTestData.Definition(), RunOptions.Default);

        CollectionAssert.AreEquivalent(new[] { "g1", "g2", "a1", "a2" }, RunPlanTroops.AllTroopIds(plan).ToArray());
    }
}
