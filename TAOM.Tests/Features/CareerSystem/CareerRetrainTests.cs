using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Tests.Features.CareerSystem;

// #769 E7 + E12 — paid retrain and tree-version migration, both living in the lifecycle service.
// Neither may call ClearCareer: that wipes the quest tier unlocks and the flags with the choices.
[TestClass]
public class CareerRetrainTests
{
    private const string Hero = "main_hero";

    private CareerDataService _dataService;
    private ICareerRegistry _registry;
    private ICareerPassiveService _passiveService;
    private ICareerPurseAdapter _purse;
    private IModLogger _logger;
    private CareerLifecycleService _sut;

    private static readonly CareerChoiceDefinition ForkA = Choice("fork_a", "r_fork");
    private static readonly CareerChoiceDefinition ForkB = Choice("fork_b", "r_fork");
    private static readonly CareerChoiceDefinition FreeA = Choice("free_a", "r_free");
    private static readonly CareerChoiceDefinition FreeB = Choice("free_b", "r_free");

    private static CareerChoiceDefinition Choice(string id, string groupId) => new CareerChoiceDefinition(
        id: id, groupId: groupId, type: ChoiceType.Passive, description: id,
        iconSprite: "", passives: null, mutations: null);

    private static CareerDefinition Career(int treeVersion, params string[] groupIds) => new CareerDefinition(
        id: "ranger", displayName: "Ranger", description: "", portraitSprite: "",
        abilityTemplateId: "ambush", minClanTier: 0, rootChoiceId: "ranger_root",
        eligibleCultureIds: new List<string>(), choiceGroupIds: new List<string>(groupIds),
        treeVersion: treeVersion);

    [TestInitialize]
    public void Setup()
    {
        _dataService = new CareerDataService();
        _registry = Substitute.For<ICareerRegistry>();
        _passiveService = Substitute.For<ICareerPassiveService>();
        _purse = Substitute.For<ICareerPurseAdapter>();
        _logger = Substitute.For<IModLogger>();

        _purse.ChargeGold(Arg.Any<int>()).Returns(true);
        _registry.GetRetrainCost(Arg.Any<int>()).Returns(ci => 1000 + 250 * Math.Max(1, ci.Arg<int>()));

        _registry.GetGroup("r_fork").Returns(new CareerChoiceGroupDefinition(
            "r_fork", "ranger", 1, new List<string> { "fork_a", "fork_b" }, maxPicks: 1));
        _registry.GetChoicesForGroup("r_fork").Returns(new List<CareerChoiceDefinition> { ForkA, ForkB });
        _registry.GetGroup("r_free").Returns(new CareerChoiceGroupDefinition(
            "r_free", "ranger", 1, new List<string> { "free_a", "free_b" }));
        _registry.GetChoicesForGroup("r_free").Returns(new List<CareerChoiceDefinition> { FreeA, FreeB });

        UseCareer(Career(1, "r_fork", "r_free"));

        _sut = new CareerLifecycleService(_dataService, _registry,
            Substitute.For<ICareerCreationHandler>(), _passiveService, _purse, _logger);
    }

    private void UseCareer(CareerDefinition career) => _registry.GetCareer("ranger").Returns(career);

    private void GiveHero(params string[] choiceIds)
    {
        _dataService.SetCareer(Hero, "ranger");
        foreach (var id in choiceIds)
            _dataService.TryAddChoice(Hero, id, 100);
    }

    private bool Has(string choiceId) => _dataService.GetOrCreateData(Hero).HasChoice(choiceId);

    // ── RefundAll ──

    [TestMethod]
    public void RefundAll_RemovesEveryChoiceButTheRootAndCountsThem()
    {
        GiveHero("ranger_root", "fork_a", "free_a", "free_b");

        var removed = _sut.RefundAll(Hero);

        Assert.AreEqual(3, removed);
        Assert.AreEqual(1, _dataService.GetChoiceCount(Hero));
        Assert.IsTrue(Has("ranger_root"));
    }

    [TestMethod]
    public void RefundAll_KeepsTheCareerTierUnlocksAndFlags()
    {
        GiveHero("ranger_root", "fork_a");
        _dataService.UnlockTier(Hero, 2);
        _dataService.SetFlag(Hero, "quest_flag");

        _sut.RefundAll(Hero);

        Assert.AreEqual("ranger", _dataService.GetCareerStringId(Hero));
        Assert.IsTrue(_dataService.IsTierUnlocked(Hero, 2));
        Assert.IsTrue(_dataService.HasFlag(Hero, "quest_flag"));
    }

    [TestMethod]
    public void RefundAll_MissingRoot_IsAddedBack()
    {
        GiveHero("fork_a", "free_a");

        var removed = _sut.RefundAll(Hero);

        Assert.AreEqual(2, removed);
        Assert.IsTrue(Has("ranger_root"));
        Assert.AreEqual(1, _dataService.GetChoiceCount(Hero));
    }

    [TestMethod]
    public void RefundAll_UnknownStoredIds_AreRemoved()
    {
        GiveHero("ranger_root", "retired_choice");

        Assert.AreEqual(1, _sut.RefundAll(Hero));
        Assert.IsFalse(Has("retired_choice"));
    }

    [TestMethod]
    public void RefundAll_OnlyTheRoot_RemovesNothing()
    {
        GiveHero("ranger_root");

        Assert.AreEqual(0, _sut.RefundAll(Hero));
        Assert.IsTrue(Has("ranger_root"));
    }

    [TestMethod]
    public void RefundAll_HeroWithoutACareer_ChangesNothing()
    {
        _dataService.GetOrCreateData(Hero).AddChoice("stray");

        Assert.AreEqual(0, _sut.RefundAll(Hero));
        Assert.IsTrue(Has("stray"));
    }

    [TestMethod]
    public void RefundAll_CareerUnknownToTheRegistry_ChangesNothing()
    {
        GiveHero("ranger_root", "fork_a");
        UseCareer(null);

        Assert.AreEqual(0, _sut.RefundAll(Hero));
        Assert.IsTrue(Has("fork_a"));
    }

    // ── TryRetrain ──

    [TestMethod]
    public void TryRetrain_NoCareer_IsNoCareerAndChargesNothing()
    {
        Assert.AreEqual(CareerRetrainResult.NoCareer, _sut.TryRetrain(Hero, 5));
        _purse.DidNotReceive().ChargeGold(Arg.Any<int>());
    }

    [TestMethod]
    public void TryRetrain_NoForkPick_IsNothingToRetrainAndChargesNothing()
    {
        GiveHero("ranger_root", "free_a");

        Assert.AreEqual(CareerRetrainResult.NothingToRetrain, _sut.TryRetrain(Hero, 5));
        _purse.DidNotReceive().ChargeGold(Arg.Any<int>());
        Assert.IsTrue(Has("free_a"));
    }

    [TestMethod]
    public void TryRetrain_ShortOfGold_ChangesNothing()
    {
        GiveHero("ranger_root", "fork_a", "free_a");
        _dataService.SetFlag(Hero, "keep");
        _purse.ChargeGold(Arg.Any<int>()).Returns(false);

        var result = _sut.TryRetrain(Hero, 5);

        Assert.AreEqual(CareerRetrainResult.NotEnoughGold, result);
        Assert.IsTrue(Has("fork_a"));
        Assert.IsTrue(Has("free_a"));
        Assert.IsTrue(_dataService.HasFlag(Hero, "keep"));
        _passiveService.DidNotReceive().RefreshCache(Arg.Any<ICareerDataService>(), Arg.Any<ICareerRegistry>());
    }

    [TestMethod]
    public void TryRetrain_Done_ChargesExactlyTheCostRefundsAndRefreshesTheCache()
    {
        GiveHero("ranger_root", "fork_a", "free_a");
        _dataService.UnlockTier(Hero, 2);

        var result = _sut.TryRetrain(Hero, 5);

        Assert.AreEqual(CareerRetrainResult.Done, result);
        _purse.Received(1).ChargeGold(2250); // 1000 + 250 * 5
        Assert.AreEqual(1, _dataService.GetChoiceCount(Hero));
        Assert.IsTrue(Has("ranger_root"));
        Assert.AreEqual("ranger", _dataService.GetCareerStringId(Hero));
        Assert.IsTrue(_dataService.IsTierUnlocked(Hero, 2));
        _passiveService.Received(1).RefreshCache(_dataService, _registry);
    }

    // ── MigrateTreeVersion ──

    [TestMethod]
    public void MigrateTreeVersion_LegacyCareer_IsANoOp()
    {
        GiveHero("ranger_root", "fork_a");

        Assert.AreEqual(0, _sut.MigrateTreeVersion(Hero));
        Assert.IsTrue(Has("fork_a"));
        Assert.AreEqual(0, _dataService.GetOrCreateData(Hero).Flags.Count);
    }

    [TestMethod]
    public void MigrateTreeVersion_NoCareer_IsZero()
    {
        Assert.AreEqual(0, _sut.MigrateTreeVersion(Hero));
    }

    [TestMethod]
    public void MigrateTreeVersion_CareerUnknownToTheRegistry_IsZeroAndKeepsEverything()
    {
        GiveHero("ranger_root", "fork_a");
        UseCareer(null);

        Assert.AreEqual(0, _sut.MigrateTreeVersion(Hero));
        Assert.IsTrue(Has("fork_a"));
    }

    [TestMethod]
    public void MigrateTreeVersion_StoredVersionAlreadyCurrent_IsANoOp()
    {
        UseCareer(Career(2, "r_fork", "r_free"));
        GiveHero("ranger_root", "fork_a");
        _dataService.SetFlag(Hero, CareerTreeVersion.FlagFor(2));

        Assert.AreEqual(0, _sut.MigrateTreeVersion(Hero));
        Assert.IsTrue(Has("fork_a"));
    }

    [TestMethod]
    public void MigrateTreeVersion_StoredVersionNewerThanTheTree_IsANoOp()
    {
        UseCareer(Career(2, "r_fork", "r_free"));
        GiveHero("ranger_root", "fork_a");
        _dataService.SetFlag(Hero, CareerTreeVersion.FlagFor(3));

        Assert.AreEqual(0, _sut.MigrateTreeVersion(Hero));
        Assert.IsTrue(Has("fork_a"));
    }

    [TestMethod]
    public void MigrateTreeVersion_CareerListsNoGroups_KeepsEverythingAndSetsNoFlag()
    {
        UseCareer(Career(2));
        GiveHero("ranger_root", "fork_a");

        Assert.AreEqual(0, _sut.MigrateTreeVersion(Hero));
        Assert.IsTrue(Has("fork_a"));
        Assert.IsFalse(_dataService.HasFlag(Hero, CareerTreeVersion.FlagFor(2)));
        _logger.Received().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void MigrateTreeVersion_ListedGroupDoesNotResolve_KeepsEverythingAndSetsNoFlag()
    {
        UseCareer(Career(2, "r_fork", "r_missing"));
        GiveHero("ranger_root", "fork_a");

        Assert.AreEqual(0, _sut.MigrateTreeVersion(Hero));
        Assert.IsTrue(Has("fork_a"));
        Assert.IsFalse(_dataService.HasFlag(Hero, CareerTreeVersion.FlagFor(2)));
        _logger.Received().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void MigrateTreeVersion_ListedGroupHasNoChoices_KeepsEverythingAndSetsNoFlag()
    {
        // A malformed choices file leaves every group resolvable and empty: the tree "changed"
        // only because it failed to load, and refunding on that would be permanent.
        UseCareer(Career(2, "r_fork", "r_free"));
        _registry.GetChoicesForGroup("r_free").Returns(new List<CareerChoiceDefinition>());
        GiveHero("ranger_root", "fork_a", "free_a");

        Assert.AreEqual(0, _sut.MigrateTreeVersion(Hero));
        Assert.IsTrue(Has("fork_a"));
        Assert.IsTrue(Has("free_a"));
        Assert.IsFalse(_dataService.HasFlag(Hero, CareerTreeVersion.FlagFor(2)));
        _logger.Received().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void MigrateTreeVersion_ProvenNewTree_RefundsAndStamps()
    {
        UseCareer(Career(2, "r_fork", "r_free"));
        GiveHero("ranger_root", "fork_a", "free_a", "free_b");
        _dataService.UnlockTier(Hero, 3);
        _dataService.SetFlag(Hero, "quest_flag");

        Assert.AreEqual(3, _sut.MigrateTreeVersion(Hero));

        Assert.AreEqual(1, _dataService.GetChoiceCount(Hero));
        Assert.IsTrue(Has("ranger_root"));
        Assert.IsTrue(_dataService.HasFlag(Hero, CareerTreeVersion.FlagFor(2)));
        Assert.IsTrue(_dataService.IsTierUnlocked(Hero, 3));
        Assert.IsTrue(_dataService.HasFlag(Hero, "quest_flag"));
    }

    [TestMethod]
    public void MigrateTreeVersion_SecondCall_IsANoOp()
    {
        UseCareer(Career(2, "r_fork", "r_free"));
        GiveHero("ranger_root", "fork_a");
        Assert.AreEqual(1, _sut.MigrateTreeVersion(Hero));
        _dataService.TryAddChoice(Hero, "fork_b", 100);

        Assert.AreEqual(0, _sut.MigrateTreeVersion(Hero));
        Assert.IsTrue(Has("fork_b"), "a pick taken after the migration must survive the next load");
    }

    [TestMethod]
    public void MigrateTreeVersion_AfterCharacterCreation_DoesNotRefundANewPlayersPicks()
    {
        // Without the creation stamp, the first load after character creation would read the
        // missing flag as "old save" and refund legitimate picks.
        UseCareer(Career(2, "r_fork", "r_free"));
        _registry.GetMaxChoicesForHero(Arg.Any<CareerDefinition>(), Arg.Any<int>()).Returns(10);
        var creation = new CareerCreationHandler(_dataService, _registry, _passiveService, _logger);

        creation.OnCareerSelected(Hero, "ranger");
        _dataService.TryAddChoice(Hero, "fork_a", 100);

        Assert.AreEqual(0, _sut.MigrateTreeVersion(Hero));
        Assert.IsTrue(Has("fork_a"));
        Assert.IsTrue(Has("ranger_root"));
    }

    [TestMethod]
    public void MigrateTreeVersion_ProvenTreeHeroHoldsOnlyTheRoot_ReturnsZeroButStillSetsTheFlag()
    {
        // Nothing was refunded, so the caller must not announce a refund; the hero is still on the
        // new tree afterwards, so the flag must be set or the next load would migrate again.
        UseCareer(Career(2, "r_fork", "r_free"));
        GiveHero("ranger_root");

        Assert.AreEqual(0, _sut.MigrateTreeVersion(Hero));

        Assert.IsTrue(_dataService.HasFlag(Hero, CareerTreeVersion.FlagFor(2)));
        Assert.IsTrue(Has("ranger_root"));
    }

    [TestMethod]
    public void MigrateTreeVersion_ProvenTreeWithPicks_ReturnsTheCountRemoved()
    {
        UseCareer(Career(2, "r_fork", "r_free"));
        GiveHero("ranger_root", "fork_a", "free_a");

        Assert.AreEqual(2, _sut.MigrateTreeVersion(Hero));
    }

    [TestMethod]
    public void MigrateTreeVersion_HeroHoldingAnOlderVersionFlag_LeavesExactlyOneVersionFlag()
    {
        UseCareer(Career(3, "r_fork", "r_free"));
        GiveHero("ranger_root", "fork_a");
        _dataService.SetFlag(Hero, CareerTreeVersion.FlagFor(2));
        _dataService.SetFlag(Hero, "quest_flag");

        _sut.MigrateTreeVersion(Hero);

        var flags = _dataService.GetOrCreateData(Hero).Flags;
        CollectionAssert.AreEquivalent(new[] { "quest_flag", "career_tree_v3" }, flags);
    }

    // ── Codex review: every hero that holds career data, not only the player ──

    [TestMethod]
    public void MigrateAllTreeVersions_MigratesEveryHeroWithCareerData_AndReturnsOnlyThePlayersCount()
    {
        // A co-op hero, or one the player takes over later, keeps its career data in the save but is
        // not the main hero at session launch. Left on the old tree, its fork picks would read as
        // locked in and the player would have to pay to retrain what the migration owes for free.
        UseCareer(Career(2, "r_fork", "r_free"));
        GiveHero("ranger_root", "fork_a", "free_a");
        const string other = "coop_hero";
        _dataService.SetCareer(other, "ranger");
        _dataService.TryAddChoice(other, "ranger_root", 100);
        _dataService.TryAddChoice(other, "fork_b", 100);

        Assert.AreEqual(2, _sut.MigrateAllTreeVersions(Hero), "the count is the player's alone");

        Assert.AreEqual(1, _dataService.GetChoiceCount(other), "the other hero is migrated too");
        Assert.IsTrue(_dataService.GetOrCreateData(other).HasChoice("ranger_root"));
        Assert.IsTrue(_dataService.HasFlag(other, CareerTreeVersion.FlagFor(2)));
    }

    [TestMethod]
    public void MigrateAllTreeVersions_PlayerWithoutCareerData_StillMigratesTheOthersAndReturnsZero()
    {
        UseCareer(Career(2, "r_fork", "r_free"));
        const string other = "coop_hero";
        _dataService.SetCareer(other, "ranger");
        _dataService.TryAddChoice(other, "fork_a", 100);

        Assert.AreEqual(0, _sut.MigrateAllTreeVersions(Hero));

        Assert.IsFalse(_dataService.GetOrCreateData(other).HasChoice("fork_a"));
        Assert.IsFalse(_dataService.GetAllData().ContainsKey(Hero), "no data is created for a player without a career");
    }

    // ── review: a career id the registry does not know ──

    [TestMethod]
    public void TryRetrain_CareerIdUnknownToTheRegistry_IsNoCareerAndChargesNothing()
    {
        GiveHero("ranger_root", "fork_a");
        UseCareer(null);

        Assert.AreEqual(CareerRetrainResult.NoCareer, _sut.TryRetrain(Hero, 5));

        _purse.DidNotReceive().ChargeGold(Arg.Any<int>());
        Assert.IsTrue(Has("fork_a"));
    }
}
