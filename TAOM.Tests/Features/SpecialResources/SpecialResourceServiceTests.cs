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
        kingdomId: "mordor",
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

        _config.GetByKingdomId("mordor").Returns(MordorResource);
    }

    [TestMethod]
    public void EarnFromBattle_AddsScaledAmount_BasedOnEnemySizeRatio()
    {
        _storage.Get("hero1").Returns(100f);
        _service.EarnFromBattle("hero1", "mordor", 1.5f);
        _storage.Received(1).Set("hero1", 115f); // 100 + 10*1.5
    }

    [TestMethod]
    public void EarnFromBattle_ClampsRatio_ToMinHalf()
    {
        _storage.Get("hero1").Returns(100f);
        _service.EarnFromBattle("hero1", "mordor", 0.1f);
        _storage.Received(1).Set("hero1", 105f); // 100 + 10*0.5 (min clamp)
    }

    [TestMethod]
    public void EarnFromBattle_ClampsRatio_ToMaxTwo()
    {
        _storage.Get("hero1").Returns(100f);
        _service.EarnFromBattle("hero1", "mordor", 5.0f);
        _storage.Received(1).Set("hero1", 120f); // 100 + 10*2.0 (max clamp)
    }

    [TestMethod]
    public void EarnFromBattle_CapsAtResourceMax()
    {
        _storage.Get("hero1").Returns(498f);
        _service.EarnFromBattle("hero1", "mordor", 1.0f);
        _storage.Received(1).Set("hero1", 500f); // capped at 500
    }

    [TestMethod]
    public void EarnFromBattle_NoOp_WhenKingdomHasNoResource()
    {
        _config.GetByKingdomId("gondor").Returns((SpecialResource)null);
        _service.EarnFromBattle("hero1", "gondor", 1.0f);
        _storage.DidNotReceive().Set(Arg.Any<string>(), Arg.Any<float>());
    }

    [TestMethod]
    public void EarnFromRaid_AddsConfiguredAmount()
    {
        _storage.Get("hero1").Returns(50f);
        _service.EarnFromRaid("hero1", "mordor");
        _storage.Received(1).Set("hero1", 58f); // 50 + 8
    }

    [TestMethod]
    public void EarnFromSiege_AddsConfiguredAmount()
    {
        _storage.Get("hero1").Returns(50f);
        _service.EarnFromSiege("hero1", "mordor");
        _storage.Received(1).Set("hero1", 65f); // 50 + 15
    }

    [TestMethod]
    public void EarnFromPrisoners_AddsPerPrisonerTimesCount()
    {
        _storage.Get("hero1").Returns(50f);
        _service.EarnFromPrisoners("hero1", "mordor", 7);
        _storage.Received(1).Set("hero1", 57f); // 50 + 1*7
    }

    [TestMethod]
    public void CanAffordUpgrade_ReturnsFalse_WhenInsufficientResources()
    {
        var cost = new TroopResourceCostEntry("mordor_uruk_deathwarden", "scraps", 5, 0.3f);
        _config.GetTroopCost("mordor_uruk_deathwarden").Returns(cost);
        _storage.Get("hero1").Returns(8f);

        Assert.IsFalse(_service.CanAffordUpgrade("hero1", "mordor_uruk_deathwarden", 2)); // needs 10, has 8
    }

    [TestMethod]
    public void CanAffordUpgrade_ReturnsTrue_WhenSufficientResources()
    {
        var cost = new TroopResourceCostEntry("mordor_uruk_deathwarden", "scraps", 5, 0.3f);
        _config.GetTroopCost("mordor_uruk_deathwarden").Returns(cost);
        _storage.Get("hero1").Returns(15f);

        Assert.IsTrue(_service.CanAffordUpgrade("hero1", "mordor_uruk_deathwarden", 2)); // needs 10, has 15
    }

    [TestMethod]
    public void CanAffordUpgrade_ReturnsTrue_WhenTroopHasNoCost()
    {
        _config.GetTroopCost("gondor_soldier").Returns((TroopResourceCostEntry)null);
        _storage.Get("hero1").Returns(0f);

        Assert.IsTrue(_service.CanAffordUpgrade("hero1", "gondor_soldier", 10));
    }

    [TestMethod]
    public void SpendForUpgrade_DeductsCorrectAmount()
    {
        var cost = new TroopResourceCostEntry("mordor_uruk_captain", "scraps", 4, 0.2f);
        _config.GetTroopCost("mordor_uruk_captain").Returns(cost);

        _service.SpendForUpgrade("hero1", "mordor_uruk_captain", 3);
        _storage.Received(1).Add("hero1", -12f); // 4 * 3
    }

    [TestMethod]
    public void ApplyDailyTick_AppliesNetEarningMinusUpkeep()
    {
        _storage.Get("hero1").Returns(100f);

        var upkeepCost = new TroopResourceCostEntry("mordor_uruk_darkblade", "scraps", 2, 0.1f);
        _config.GetTroopCost("mordor_uruk_darkblade").Returns(upkeepCost);

        var troops = new List<TroopUpkeepInfo>
        {
            new("mordor_uruk_darkblade", 10) // 10 * 0.1 = 1.0 upkeep
        };

        // 2 towns * 0.5 = 1.0 earning, 1.0 upkeep = 0.0 net
        _service.ApplyDailyTick("hero1", "mordor", 2, troops);
        _storage.Received(1).Set("hero1", 100f); // no change
    }

    [TestMethod]
    public void ApplyDailyTick_EarningExceedsUpkeep_AddsCapped()
    {
        _storage.Get("hero1").Returns(100f);

        var troops = new List<TroopUpkeepInfo>(); // no upkeep

        // 4 towns * 0.5 = 2.0 earning
        _service.ApplyDailyTick("hero1", "mordor", 4, troops);
        _storage.Received(1).Set("hero1", 102f);
    }

    [TestMethod]
    public void ApplyDailyTick_UpkeepExceedsEarning_SubtractsFromStorage()
    {
        _storage.Get("hero1").Returns(100f);

        var upkeepCost = new TroopResourceCostEntry("mordor_uruk_deathwarden", "scraps", 5, 0.3f);
        _config.GetTroopCost("mordor_uruk_deathwarden").Returns(upkeepCost);

        var troops = new List<TroopUpkeepInfo>
        {
            new("mordor_uruk_deathwarden", 20) // 20 * 0.3 = 6.0 upkeep
        };

        // 0 towns * 0.5 = 0.0 earning, 6.0 upkeep = -6.0 net
        _service.ApplyDailyTick("hero1", "mordor", 0, troops);
        _storage.Received(1).Add("hero1", -6f);
    }

    [TestMethod]
    public void GetDailyEarning_ReturnsPerTownTimesCount()
    {
        Assert.AreEqual(1.5f, _service.GetDailyEarning("mordor", 3)); // 0.5 * 3
    }

    [TestMethod]
    public void GetDailyUpkeep_SumsTroopCosts()
    {
        var cost1 = new TroopResourceCostEntry("troop_a", "scraps", 2, 0.1f);
        var cost2 = new TroopResourceCostEntry("troop_b", "scraps", 3, 0.2f);
        _config.GetTroopCost("troop_a").Returns(cost1);
        _config.GetTroopCost("troop_b").Returns(cost2);

        var troops = new List<TroopUpkeepInfo>
        {
            new("troop_a", 5),  // 5 * 0.1 = 0.5
            new("troop_b", 10)  // 10 * 0.2 = 2.0
        };

        Assert.AreEqual(2.5f, _service.GetDailyUpkeep(troops));
    }
}
