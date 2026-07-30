using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.SpecialResources;
using TAOM.Features.SpecialResources.Domain;

namespace TAOM.Tests.Features.SpecialResources;

/// <summary>
/// Covers <see cref="ISpecialResourceService.GrantAmount"/> — the debug/cheat grant behind
/// <c>taom.add_special_resources</c>. Uses a REAL <see cref="SpecialResourceStorageService"/>
/// (not a substitute) because the floor-at-0 clamp is the storage's behavior and the cap clamp
/// is the service's — a mocked storage would hide half the contract under test.
/// </summary>
[TestClass]
public class SpecialResourceServiceGrantTests
{
    private const string HeroId = "hero_player";

    private ISpecialResourceConfigProvider _config;
    private SpecialResourceStorageService _storage;
    private IModLogger _logger;
    private SpecialResourceService _service;

    private static readonly SpecialResource MordorResource = new(
        id: "war_spoils",
        kingdomIds: new[] { "empire_s" },
        cultureIds: new[] { "mordor" },
        displayName: "War Spoils",
        iconSpriteName: "taom_war_spoils_icon",
        cap: 500f,
        startingAmount: 30f,
        dailyPerTown: 0.5f,
        perBattleVictoryBase: 10f,
        perRaid: 8f,
        perSiegeVictory: 15f,
        perPrisoner: 1f);

    [TestInitialize]
    public void Setup()
    {
        _config = Substitute.For<ISpecialResourceConfigProvider>();
        _storage = new SpecialResourceStorageService();
        _logger = Substitute.For<IModLogger>();
        _service = new SpecialResourceService(_config, _storage, _logger);

        _config.GetByKingdomId("empire_s").Returns(MordorResource);
        _config.GetByCultureId("mordor").Returns(MordorResource);
    }

    [TestMethod]
    public void GrantAmount_PositiveAmount_AddsToBalanceAndReportsBeforeAfter()
    {
        // Arrange
        _storage.Set(HeroId, "war_spoils", 120f);

        // Act
        var result = _service.GrantAmount(HeroId, "empire_s", "mordor", 200f);

        // Assert
        Assert.IsTrue(result.Resolved);
        Assert.AreEqual("war_spoils", result.ResourceId);
        Assert.AreEqual("War Spoils", result.DisplayName);
        Assert.AreEqual(120f, result.Before);
        Assert.AreEqual(320f, result.After);
        Assert.AreEqual(500f, result.Cap);
        Assert.AreEqual(320f, _storage.Get(HeroId, "war_spoils"));
    }

    [TestMethod]
    public void GrantAmount_ExceedsCap_ClampsToCap()
    {
        // Arrange
        _storage.Set(HeroId, "war_spoils", 480f);

        // Act
        var result = _service.GrantAmount(HeroId, "empire_s", "mordor", 999999f);

        // Assert
        Assert.AreEqual(500f, result.After);
        Assert.AreEqual(500f, _storage.Get(HeroId, "war_spoils"));
    }

    [TestMethod]
    public void GrantAmount_AlreadyAtCap_LeavesBalanceUnchanged()
    {
        // Arrange
        _storage.Set(HeroId, "war_spoils", 500f);

        // Act
        var result = _service.GrantAmount(HeroId, "empire_s", "mordor", 250f);

        // Assert
        Assert.AreEqual(result.Before, result.After);
        Assert.AreEqual(500f, _storage.Get(HeroId, "war_spoils"));
    }

    [TestMethod]
    public void GrantAmount_NegativeAmount_DeductsFromBalance()
    {
        // Arrange
        _storage.Set(HeroId, "war_spoils", 300f);

        // Act
        var result = _service.GrantAmount(HeroId, "empire_s", "mordor", -100f);

        // Assert
        Assert.AreEqual(200f, result.After);
        Assert.AreEqual(200f, _storage.Get(HeroId, "war_spoils"));
    }

    [TestMethod]
    public void GrantAmount_NegativeAmountBelowZero_FloorsAtZero()
    {
        // Arrange
        _storage.Set(HeroId, "war_spoils", 40f);

        // Act
        var result = _service.GrantAmount(HeroId, "empire_s", "mordor", -500f);

        // Assert
        Assert.AreEqual(0f, result.After);
        Assert.AreEqual(0f, _storage.Get(HeroId, "war_spoils"));
    }

    [TestMethod]
    public void GrantAmount_NoResourceResolves_ReturnsUnresolvedAndLeavesStorageUntouched()
    {
        // Act
        var result = _service.GrantAmount(HeroId, "no_such_kingdom", "no_such_culture", 500f);

        // Assert
        Assert.IsFalse(result.Resolved);
        Assert.AreEqual(0, _storage.GetAllData().Count);
    }

    /// <summary>
    /// The console is usable while the party screen is open, so a grant can land mid-session while
    /// an upgrade spend is still pending. `CommitSession` debits with a RELATIVE `Add(-pending)`,
    /// never an absolute `Set`, so the grant must fold in and be debited exactly once.
    /// </summary>
    [TestMethod]
    public void GrantAmount_DuringOpenPartyScreenSession_FoldsIntoBalanceAndCommitDebitsOnce()
    {
        // Arrange — 100 on hand, two queued upgrades at 100 each
        _storage.Set(HeroId, "war_spoils", 100f);
        _config.GetTroopCost("mordor_elite").Returns(
            new TroopResourceCostEntry("mordor_elite", "war_spoils", upgradeCost: 100, dailyUpkeep: 0f));
        _service.BeginPartyScreenSession();
        _service.QueueUpgradeSpend(HeroId, "mordor_elite", 2);

        // Act — cheat mid-session, then close the screen
        _service.GrantAmount(HeroId, "empire_s", "mordor", 500f);
        _service.CommitSession(HeroId, "empire_s", "mordor");

        // Assert — 100 + 500 = 600, capped to 500, minus the 200 pending = 300
        Assert.AreEqual(300f, _storage.Get(HeroId, "war_spoils"));
    }

    [TestMethod]
    public void GrantAmount_NaNAmount_ReturnsUnresolvedAndLeavesBalanceUntouched()
    {
        // Arrange
        _storage.Set(HeroId, "war_spoils", 120f);

        // Act
        var result = _service.GrantAmount(HeroId, "empire_s", "mordor", float.NaN);

        // Assert
        Assert.IsFalse(result.Resolved);
        Assert.AreEqual(120f, _storage.Get(HeroId, "war_spoils"));
    }

    [TestMethod]
    public void GrantAmount_InfiniteAmount_ReturnsUnresolvedAndLeavesBalanceUntouched()
    {
        // Arrange
        _storage.Set(HeroId, "war_spoils", 120f);

        // Act
        var result = _service.GrantAmount(HeroId, "empire_s", "mordor", float.PositiveInfinity);

        // Assert
        Assert.IsFalse(result.Resolved);
        Assert.AreEqual(120f, _storage.Get(HeroId, "war_spoils"));
    }
}
