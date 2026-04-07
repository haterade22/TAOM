using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.SpecialResources;
using TAOM.Features.SpecialResources.Domain;

namespace TAOM.Tests.Features.SpecialResources;

[TestClass]
public class SpecialResourceServiceTests
{
    private ISpecialResourceConfigProvider _config;
    private ISpecialResourceStorageService _storage;
    private SpecialResourceService _service;

    private static readonly SpecialResource MordorResource = new(
        id: "scraps",
        kingdomId: "empire_s",
        cultureId: "mordor",
        displayName: "Scraps",
        iconSpriteName: "taom_scraps_icon",
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
        _storage = Substitute.For<ISpecialResourceStorageService>();
        _service = new SpecialResourceService(_config, _storage);

        _config.GetByKingdomId("empire_s").Returns(MordorResource);
        _config.GetByCultureId("mordor").Returns(MordorResource);
    }

    // ── Resolution ──

    [TestMethod]
    public void ResolveResource_PrefersKingdom_OverCulture()
    {
        var result = _service.ResolveResource("empire_s", "mordor");
        Assert.AreSame(MordorResource, result);
    }

    [TestMethod]
    public void ResolveResource_FallsToCulture_WhenKingdomIsNull()
    {
        var result = _service.ResolveResource(null, "mordor");
        Assert.AreSame(MordorResource, result);
    }

    [TestMethod]
    public void ResolveResource_ReturnsNull_WhenBothNull()
    {
        var result = _service.ResolveResource(null, null);
        Assert.IsNull(result);
    }

    [TestMethod]
    public void ResolveResource_FallsToCulture_WhenKingdomNotConfigured()
    {
        _config.GetByKingdomId("empire_w").Returns((SpecialResource)null);
        var result = _service.ResolveResource("empire_w", "mordor");
        Assert.AreSame(MordorResource, result);
    }

    // ── Earning ──

    [TestMethod]
    public void EarnFromBattle_AddsScaledAmount_BasedOnEnemySizeRatio()
    {
        _storage.Get("hero1", "scraps").Returns(100f);
        _service.EarnFromBattle("hero1", "empire_s", null, 1.5f);
        _storage.Received(1).Set("hero1", "scraps", 115f);
    }

    [TestMethod]
    public void EarnFromBattle_WorksViaCultureFallback()
    {
        _storage.Get("hero1", "scraps").Returns(100f);
        _service.EarnFromBattle("hero1", null, "mordor", 1.0f);
        _storage.Received(1).Set("hero1", "scraps", 110f);
    }

    [TestMethod]
    public void EarnFromBattle_ClampsRatio_ToMinHalf()
    {
        _storage.Get("hero1", "scraps").Returns(100f);
        _service.EarnFromBattle("hero1", "empire_s", null, 0.1f);
        _storage.Received(1).Set("hero1", "scraps", 105f);
    }

    [TestMethod]
    public void EarnFromBattle_ClampsRatio_ToMaxTwo()
    {
        _storage.Get("hero1", "scraps").Returns(100f);
        _service.EarnFromBattle("hero1", "empire_s", null, 5.0f);
        _storage.Received(1).Set("hero1", "scraps", 120f);
    }

    [TestMethod]
    public void EarnFromBattle_CapsAtResourceMax()
    {
        _storage.Get("hero1", "scraps").Returns(498f);
        _service.EarnFromBattle("hero1", "empire_s", null, 1.0f);
        _storage.Received(1).Set("hero1", "scraps", 500f);
    }

    [TestMethod]
    public void EarnFromBattle_NoOp_WhenNoResourceResolved()
    {
        _config.GetByKingdomId("empire_w").Returns((SpecialResource)null);
        _config.GetByCultureId("gondor").Returns((SpecialResource)null);
        _service.EarnFromBattle("hero1", "empire_w", "gondor", 1.0f);
        _storage.DidNotReceive().Set(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>());
    }

    [TestMethod]
    public void EarnFromRaid_AddsConfiguredAmount()
    {
        _storage.Get("hero1", "scraps").Returns(50f);
        _service.EarnFromRaid("hero1", "empire_s", null);
        _storage.Received(1).Set("hero1", "scraps", 58f);
    }

    [TestMethod]
    public void EarnFromSiege_AddsConfiguredAmount()
    {
        _storage.Get("hero1", "scraps").Returns(50f);
        _service.EarnFromSiege("hero1", "empire_s", null);
        _storage.Received(1).Set("hero1", "scraps", 65f);
    }

    [TestMethod]
    public void EarnFromPrisoners_AddsPerPrisonerTimesCount()
    {
        _storage.Get("hero1", "scraps").Returns(50f);
        _service.EarnFromPrisoners("hero1", "empire_s", null, 7);
        _storage.Received(1).Set("hero1", "scraps", 57f);
    }

    // ── Upgrade Affordability ──

    [TestMethod]
    public void CanAffordUpgrade_ReturnsFalse_WhenInsufficientResources()
    {
        var cost = new TroopResourceCostEntry("mordor_uruk_deathwarden", "scraps", 5, 0.3f);
        _config.GetTroopCost("mordor_uruk_deathwarden").Returns(cost);
        _storage.Get("hero1", "scraps").Returns(8f);

        Assert.IsFalse(_service.CanAffordUpgrade("hero1", "empire_s", null, "mordor_uruk_deathwarden", 2));
    }

    [TestMethod]
    public void CanAffordUpgrade_ReturnsTrue_WhenSufficientResources()
    {
        var cost = new TroopResourceCostEntry("mordor_uruk_deathwarden", "scraps", 5, 0.3f);
        _config.GetTroopCost("mordor_uruk_deathwarden").Returns(cost);
        _storage.Get("hero1", "scraps").Returns(15f);

        Assert.IsTrue(_service.CanAffordUpgrade("hero1", "empire_s", null, "mordor_uruk_deathwarden", 2));
    }

    [TestMethod]
    public void SpendForUpgrade_DeductsCorrectAmount()
    {
        var cost = new TroopResourceCostEntry("mordor_uruk_captain", "scraps", 4, 0.2f);
        _config.GetTroopCost("mordor_uruk_captain").Returns(cost);

        _service.SpendForUpgrade("hero1", "empire_s", null, "mordor_uruk_captain", 3);
        _storage.Received(1).Add("hero1", "scraps", -12f);
    }

    // ── Daily Tick ──

    [TestMethod]
    public void ApplyDailyTick_EarningExceedsUpkeep_AddsCapped()
    {
        _storage.Get("hero1", "scraps").Returns(100f);
        _service.ApplyDailyTick("hero1", "empire_s", null, 4, new List<TroopUpkeepInfo>());
        _storage.Received(1).Set("hero1", "scraps", 102f);
    }

    [TestMethod]
    public void ApplyDailyTick_UpkeepExceedsEarning_SubtractsFromStorage()
    {
        _storage.Get("hero1", "scraps").Returns(100f);
        var upkeepCost = new TroopResourceCostEntry("mordor_uruk_deathwarden", "scraps", 5, 0.3f);
        _config.GetTroopCost("mordor_uruk_deathwarden").Returns(upkeepCost);
        var troops = new List<TroopUpkeepInfo> { new("mordor_uruk_deathwarden", 20) };

        _service.ApplyDailyTick("hero1", "empire_s", null, 0, troops);
        _storage.Received(1).Add("hero1", "scraps", -6f);
    }

    [TestMethod]
    public void GetDailyEarning_ReturnsPerTownTimesCount()
    {
        Assert.AreEqual(1.5f, _service.GetDailyEarning("empire_s", null, 3));
    }

    // ── Pending Transaction ──

    [TestMethod]
    public void QueueUpgradeSpend_DoesNotMutateStorage()
    {
        var cost = new TroopResourceCostEntry("mordor_uruk_captain", "scraps", 4, 0.2f);
        _config.GetTroopCost("mordor_uruk_captain").Returns(cost);

        _service.BeginPartyScreenSession();
        _service.QueueUpgradeSpend("hero1", "mordor_uruk_captain", 3);

        _storage.DidNotReceive().Add(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>());
        _storage.DidNotReceive().Set(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>());
    }

    [TestMethod]
    public void GetAvailableAfterPending_SubtractsPendingFromStorage()
    {
        var cost = new TroopResourceCostEntry("mordor_uruk_captain", "scraps", 4, 0.2f);
        _config.GetTroopCost("mordor_uruk_captain").Returns(cost);
        _storage.Get("hero1", "scraps").Returns(100f);

        _service.BeginPartyScreenSession();
        _service.QueueUpgradeSpend("hero1", "mordor_uruk_captain", 3);

        Assert.AreEqual(88f, _service.GetAvailableAfterPending("hero1", "empire_s", null));
    }

    [TestMethod]
    public void CommitSession_AppliesPendingToStorage()
    {
        var cost = new TroopResourceCostEntry("mordor_uruk_captain", "scraps", 4, 0.2f);
        _config.GetTroopCost("mordor_uruk_captain").Returns(cost);

        _service.BeginPartyScreenSession();
        _service.QueueUpgradeSpend("hero1", "mordor_uruk_captain", 3);
        _service.CommitSession("hero1", "empire_s", null);

        _storage.Received(1).Add("hero1", "scraps", -12f);
    }

    [TestMethod]
    public void CancelSession_DiscardsAndNeverMutatesStorage()
    {
        var cost = new TroopResourceCostEntry("mordor_uruk_captain", "scraps", 4, 0.2f);
        _config.GetTroopCost("mordor_uruk_captain").Returns(cost);

        _service.BeginPartyScreenSession();
        _service.QueueUpgradeSpend("hero1", "mordor_uruk_captain", 3);
        _service.CancelSession();

        _storage.DidNotReceive().Add(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>());
    }

    [TestMethod]
    public void ClampUpgradeCount_LimitsByAvailableMinusPending()
    {
        var cost = new TroopResourceCostEntry("mordor_uruk_captain", "scraps", 4, 0.2f);
        _config.GetTroopCost("mordor_uruk_captain").Returns(cost);
        _storage.Get("hero1", "scraps").Returns(10f);

        _service.BeginPartyScreenSession();
        Assert.AreEqual(2, _service.ClampUpgradeCount("hero1", "empire_s", null, "mordor_uruk_captain", 5));
    }

    [TestMethod]
    public void CommitSession_NoOp_WhenNotInSession()
    {
        _service.CommitSession("hero1", "empire_s", null);
        _storage.DidNotReceive().Add(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>());
    }

    // ── Edge Cases ──

    [TestMethod]
    public void EarnFromRaid_NoOp_WhenBothIdsNull()
    {
        _service.EarnFromRaid("hero1", null, null);
        _storage.DidNotReceive().Set(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>());
    }

    [TestMethod]
    public void InitializeHero_SetsStartingAmount()
    {
        _service.InitializeHero("hero1", "empire_s", null);
        _storage.Received(1).Set("hero1", "scraps", 30f);
    }

    [TestMethod]
    public void InitializeHero_WorksViaCulture()
    {
        _service.InitializeHero("hero1", null, "mordor");
        _storage.Received(1).Set("hero1", "scraps", 30f);
    }

    [TestMethod]
    public void GetCurrentAmount_ReturnsZero_WhenNoResourceResolved()
    {
        _config.GetByKingdomId("empire_w").Returns((SpecialResource)null);
        _config.GetByCultureId("gondor").Returns((SpecialResource)null);
        Assert.AreEqual(0f, _service.GetCurrentAmount("hero1", "empire_w", "gondor"));
    }
}
