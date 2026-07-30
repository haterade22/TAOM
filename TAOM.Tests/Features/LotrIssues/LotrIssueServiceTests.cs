using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.LotrIssues;
using TAOM.Features.LotrIssues.Domain;

namespace TAOM.Tests.Features.LotrIssues;

[TestClass]
public class LotrIssueServiceTests
{
    private ILotrIssueConfigProvider _config;
    private LotrIssueService _sut;

    [TestInitialize]
    public void Setup()
    {
        _config = Substitute.For<ILotrIssueConfigProvider>();
        _config.LoadIssues().Returns(new List<LotrIssueDefinition>());
        _sut = new LotrIssueService(_config, Substitute.For<IModLogger>());
    }

    private static LotrIssueDefinition Def(
        string id = "i1",
        IssueGiverOccupation giver = IssueGiverOccupation.Headman,
        IReadOnlyList<string> cultures = null,
        int count = 10,
        float countPerDiff = 0f,
        int rewardGoldBase = 0,
        float rewardGoldPerDiff = 0f,
        int rewardRenown = 0,
        string rewardItem = "",
        int relationMin = -10)
        => new LotrIssueDefinition(id, LotrIssueTemplate.DeliverGoods, giver, IssueFrequencyTier.Common,
            cultures, count, countPerDiff, "", "", rewardGoldBase, rewardGoldPerDiff, rewardRenown,
            rewardItem, "", relationMin, new LotrIssueText("t", "d", "", "", "", "", "", "", ""));

    private static ILotrIssueGiverAdapter Giver(
        IssueGiverOccupation? occ = IssueGiverOccupation.Headman, string culture = "gondor",
        int relation = 0, bool valid = true)
    {
        var g = Substitute.For<ILotrIssueGiverAdapter>();
        g.IsValid.Returns(valid);
        g.Occupation.Returns(occ);
        g.CultureStringId.Returns(culture);
        g.RelationWithPlayer.Returns(relation);
        return g;
    }

    private void Load(params LotrIssueDefinition[] defs)
        => _config.LoadIssues().Returns(new List<LotrIssueDefinition>(defs));

    private static LotrCaptiveStack Stack(int number) => new LotrCaptiveStack(false, number);

    private static LotrCaptiveStack HeroStack(int number) => new LotrCaptiveStack(true, number);

    // ── GetEligibleIssues ────────────────────────────────────────────────────

    [TestMethod]
    public void GetEligibleIssues_MatchingOccupationCultureRelation_Included()
    {
        Load(Def(giver: IssueGiverOccupation.Headman, cultures: new[] { "gondor" }, relationMin: 0));
        var result = _sut.GetEligibleIssues(Giver(IssueGiverOccupation.Headman, "gondor", relation: 5));
        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public void GetEligibleIssues_WrongOccupation_Excluded()
    {
        Load(Def(giver: IssueGiverOccupation.Headman));
        Assert.AreEqual(0, _sut.GetEligibleIssues(Giver(IssueGiverOccupation.Lord)).Count);
    }

    [TestMethod]
    public void GetEligibleIssues_WrongCulture_Excluded()
    {
        Load(Def(cultures: new[] { "gondor" }));
        Assert.AreEqual(0, _sut.GetEligibleIssues(Giver(culture: "mordor")).Count);
    }

    [TestMethod]
    public void GetEligibleIssues_EmptyCultureList_MatchesAnyCulture()
    {
        Load(Def(cultures: null));
        Assert.AreEqual(1, _sut.GetEligibleIssues(Giver(culture: "mordor")).Count);
    }

    [TestMethod]
    public void GetEligibleIssues_RelationBelowMin_Excluded()
    {
        Load(Def(relationMin: 20));
        Assert.AreEqual(0, _sut.GetEligibleIssues(Giver(relation: 5)).Count);
    }

    [TestMethod]
    public void GetEligibleIssues_NullGiver_ReturnsEmpty()
        => Assert.AreEqual(0, _sut.GetEligibleIssues(null).Count);

    [TestMethod]
    public void GetEligibleIssues_InvalidGiver_ReturnsEmpty()
    {
        Load(Def());
        Assert.AreEqual(0, _sut.GetEligibleIssues(Giver(valid: false)).Count);
    }

    [TestMethod]
    public void GetEligibleIssues_NullOccupation_ReturnsEmpty()
    {
        Load(Def());
        Assert.AreEqual(0, _sut.GetEligibleIssues(Giver(occ: null)).Count);
    }

    // ── GetIssueById ──────────────────────────────────────────────────────────

    [TestMethod]
    public void GetIssueById_Existing_ReturnsDef()
    {
        Load(Def(id: "grain"));
        Assert.AreEqual("grain", _sut.GetIssueById("grain").Id);
    }

    [TestMethod]
    public void GetIssueById_Unknown_ReturnsNull()
    {
        Load(Def(id: "grain"));
        Assert.IsNull(_sut.GetIssueById("nope"));
    }

    [TestMethod]
    public void GetIssueById_Null_ReturnsNull()
        => Assert.IsNull(_sut.GetIssueById(null));

    // ── ComputeTargetCount / ComputeRewardGold ───────────────────────────────

    [TestMethod]
    public void ComputeTargetCount_ScalesWithDifficulty()
        => Assert.AreEqual(10 + 90, _sut.ComputeTargetCount(Def(count: 10, countPerDiff: 180f), 0.5f));

    [TestMethod]
    public void ComputeTargetCount_DifficultyAboveOne_ClampedToOne()
        => Assert.AreEqual(10 + 180, _sut.ComputeTargetCount(Def(count: 10, countPerDiff: 180f), 5f));

    [TestMethod]
    public void ComputeTargetCount_NaNDifficulty_BaseOnly()
        => Assert.AreEqual(10, _sut.ComputeTargetCount(Def(count: 10, countPerDiff: 180f), float.NaN));

    [TestMethod]
    public void ComputeRewardGold_ScalesWithDifficulty()
        => Assert.AreEqual(100 + 400, _sut.ComputeRewardGold(Def(rewardGoldBase: 100, rewardGoldPerDiff: 800f), 0.5f));

    // ── IsObjectiveSatisfied ──────────────────────────────────────────────────

    [TestMethod]
    public void IsObjectiveSatisfied_ProgressMeetsTarget_True()
        => Assert.IsTrue(_sut.IsObjectiveSatisfied(10, 10));

    [TestMethod]
    public void IsObjectiveSatisfied_ProgressBelowTarget_False()
        => Assert.IsFalse(_sut.IsObjectiveSatisfied(9, 10));

    [TestMethod]
    public void IsObjectiveSatisfied_TargetZero_False()
        => Assert.IsFalse(_sut.IsObjectiveSatisfied(5, 0));

    // ── CountDeliverableCaptives ─────────────────────────────────────────────
    //
    // Regression cover for the "Hands for the Mines" report (2026-07-30): the quest counted only
    // prisoners with Occupation.Bandit, but TAOM declares that occupation on just 8 hideout-boss
    // troops — every bandit-culture rank-and-file (dunland_peasant, balcoth_volunteer, harad_levy)
    // is occupation="Soldier". A full prison roster therefore counted as zero. The rule is now
    // "any non-hero prisoner", expressed here with no troop-type input at all.

    [TestMethod]
    public void CountDeliverableCaptives_NonHeroTroops_AreCountedRegardlessOfType()
    {
        var stacks = new[] { Stack(8), Stack(7), Stack(5) };
        Assert.AreEqual(20, _sut.CountDeliverableCaptives(stacks, 100));
    }

    [TestMethod]
    public void CountDeliverableCaptives_HeroStack_Excluded()
    {
        var stacks = new[] { HeroStack(1), Stack(3) };
        Assert.AreEqual(3, _sut.CountDeliverableCaptives(stacks, 100));
    }

    [TestMethod]
    public void CountDeliverableCaptives_MoreThanCap_ClampedToCap()
        => Assert.AreEqual(6, _sut.CountDeliverableCaptives(new[] { Stack(20) }, 6));

    [TestMethod]
    public void CountDeliverableCaptives_CapZero_ReturnsZero()
        => Assert.AreEqual(0, _sut.CountDeliverableCaptives(new[] { Stack(20) }, 0));

    [TestMethod]
    public void CountDeliverableCaptives_NonPositiveStack_Ignored()
        => Assert.AreEqual(4, _sut.CountDeliverableCaptives(new[] { Stack(0), Stack(4) }, 100));

    [TestMethod]
    public void CountDeliverableCaptives_EmptyStacks_ReturnsZero()
        => Assert.AreEqual(0, _sut.CountDeliverableCaptives(new LotrCaptiveStack[0], 10));

    [TestMethod]
    public void CountDeliverableCaptives_NullStacks_ReturnsZero()
        => Assert.AreEqual(0, _sut.CountDeliverableCaptives(null, 10));

    // ── PlanCaptiveHandover ───────────────────────────────────────────────────

    [TestMethod]
    public void PlanCaptiveHandover_EnoughAcrossStacks_TakesExactlyCount()
    {
        var plan = _sut.PlanCaptiveHandover(new[] { Stack(8), Stack(7) }, 10);
        CollectionAssert.AreEqual(new[] { 8, 2 }, new List<int>(plan));
    }

    [TestMethod]
    public void PlanCaptiveHandover_HeroStack_NeverTaken()
    {
        var plan = _sut.PlanCaptiveHandover(new[] { HeroStack(1), Stack(5) }, 3);
        CollectionAssert.AreEqual(new[] { 0, 3 }, new List<int>(plan));
    }

    [TestMethod]
    public void PlanCaptiveHandover_HeroOnlyRoster_TakesNothing()
    {
        var plan = _sut.PlanCaptiveHandover(new[] { HeroStack(1), HeroStack(1) }, 5);
        CollectionAssert.AreEqual(new[] { 0, 0 }, new List<int>(plan));
    }

    [TestMethod]
    public void PlanCaptiveHandover_FewerAvailableThanRequested_TakesAllNonHero()
    {
        var plan = _sut.PlanCaptiveHandover(new[] { Stack(2) }, 5);
        CollectionAssert.AreEqual(new[] { 2 }, new List<int>(plan));
    }

    [TestMethod]
    public void PlanCaptiveHandover_CountZero_TakesNothing()
    {
        var plan = _sut.PlanCaptiveHandover(new[] { Stack(4) }, 0);
        CollectionAssert.AreEqual(new[] { 0 }, new List<int>(plan));
    }

    [TestMethod]
    public void PlanCaptiveHandover_NullStacks_ReturnsEmpty()
        => Assert.AreEqual(0, _sut.PlanCaptiveHandover(null, 5).Count);

    // ── ApplyRewards ──────────────────────────────────────────────────────────

    [TestMethod]
    public void ApplyRewards_ValidHero_GrantsGoldRenownItem()
    {
        var hero = Substitute.For<ILotrIssueRewardAdapter>();
        hero.IsValid.Returns(true);
        _sut.ApplyRewards(Def(rewardGoldBase: 200, rewardRenown: 3, rewardItem: "sword"), 0f, hero);
        hero.Received(1).AddGold(200);
        hero.Received(1).AddRenown(3);
        hero.Received(1).AddItemToInventory("sword", 1);
    }

    [TestMethod]
    public void ApplyRewards_InvalidHero_NoOps()
    {
        var hero = Substitute.For<ILotrIssueRewardAdapter>();
        hero.IsValid.Returns(false);
        _sut.ApplyRewards(Def(rewardGoldBase: 200, rewardRenown: 3), 0f, hero);
        hero.DidNotReceive().AddGold(Arg.Any<int>());
        hero.DidNotReceive().AddRenown(Arg.Any<int>());
    }

    [TestMethod]
    public void ApplyRewards_ZeroGold_DoesNotCallAddGold()
    {
        var hero = Substitute.For<ILotrIssueRewardAdapter>();
        hero.IsValid.Returns(true);
        _sut.ApplyRewards(Def(rewardGoldBase: 0), 0f, hero);
        hero.DidNotReceive().AddGold(Arg.Any<int>());
    }
}
