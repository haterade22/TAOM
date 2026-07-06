using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;
using TAOM.Features.SpecialResources;
using TAOM.Features.SpecialResources.Domain;

namespace TAOM.Tests.Features.SpecialResources;

[TestClass]
public class SpecialResourceServiceTests
{
    private ISpecialResourceConfigProvider _config;
    private ISpecialResourceStorageService _storage;
    private IModLogger _logger;
    private ICareerPassiveService _passiveService;
    private SpecialResourceService _service;

    private static readonly SpecialResource MordorResource = new(
        id: "war_spoils",
        kingdomIds: new[] { "empire_s", "isengard" },
        cultureIds: new[] { "mordor", "isengard" },
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
        _storage = Substitute.For<ISpecialResourceStorageService>();
        _logger = Substitute.For<IModLogger>();
        _passiveService = Substitute.For<ICareerPassiveService>();
        _service = new SpecialResourceService(_config, _storage, _logger, _passiveService);

        _config.GetByKingdomId("empire_s").Returns(MordorResource);
        _config.GetByKingdomId("isengard").Returns(MordorResource);
        _config.GetByCultureId("mordor").Returns(MordorResource);
        _config.GetByCultureId("isengard").Returns(MordorResource);
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
    public void ResolveResource_SharedResource_BothKingdomsResolveSameInstance()
    {
        var fromMordor = _service.ResolveResource("empire_s", null);
        var fromIsengard = _service.ResolveResource("isengard", null);
        Assert.AreSame(fromMordor, fromIsengard);
        Assert.AreEqual("war_spoils", fromMordor.Id);
    }

    [TestMethod]
    public void ResolveResource_FallsToCulture_WhenKingdomNotConfigured()
    {
        _config.GetByKingdomId("empire_w").Returns((SpecialResource)null);
        var result = _service.ResolveResource("empire_w", "mordor");
        Assert.AreSame(MordorResource, result);
    }

    // ── Resolution Logging Dedupe ──

    [TestMethod]
    public void ResolveResource_KingdomHit_LogsDebugOnce_ForFirstCall()
    {
        _service.ResolveResource("empire_s", null);
        _logger.Received(1).LogDebug(Arg.Is<string>(s => s.Contains("via kingdom 'empire_s'")));
    }

    [TestMethod]
    public void ResolveResource_KingdomHit_DoesNotLogDebug_OnSecondIdenticalCall()
    {
        _service.ResolveResource("empire_s", null);
        _logger.ClearReceivedCalls();
        _service.ResolveResource("empire_s", null);
        _logger.DidNotReceive().LogDebug(Arg.Any<string>());
    }

    [TestMethod]
    public void ResolveResource_CultureFallback_DoesNotLogDebug_OnSecondIdenticalCall()
    {
        _service.ResolveResource(null, "mordor");
        _logger.ClearReceivedCalls();
        _service.ResolveResource(null, "mordor");
        _logger.DidNotReceive().LogDebug(Arg.Any<string>());
    }

    [TestMethod]
    public void ResolveResource_NoMatch_DoesNotLogDebug_OnSecondIdenticalCall()
    {
        _config.GetByKingdomId("unknown_kingdom").Returns((SpecialResource)null);
        _config.GetByCultureId("unknown_culture").Returns((SpecialResource)null);

        _service.ResolveResource("unknown_kingdom", "unknown_culture");
        _logger.ClearReceivedCalls();
        _service.ResolveResource("unknown_kingdom", "unknown_culture");

        _logger.DidNotReceive().LogDebug(Arg.Any<string>());
    }

    [TestMethod]
    public void ResolveResource_DifferentKeys_LogIndependently()
    {
        _service.ResolveResource("empire_s", null);
        _service.ResolveResource("isengard", null);

        _logger.Received(1).LogDebug(Arg.Is<string>(s => s.Contains("via kingdom 'empire_s'")));
        _logger.Received(1).LogDebug(Arg.Is<string>(s => s.Contains("via kingdom 'isengard'")));
    }

    [TestMethod]
    public void ResolveResource_SameKingdomDifferentCulture_LogsAgain()
    {
        // (kingdomId, cultureId) is the dedupe key; switching either side counts as a new context.
        _service.ResolveResource(null, "mordor");
        _service.ResolveResource(null, "isengard");

        _logger.Received(1).LogDebug(Arg.Is<string>(s => s.Contains("via culture 'mordor'")));
        _logger.Received(1).LogDebug(Arg.Is<string>(s => s.Contains("via culture 'isengard'")));
    }

    // ── Earning ──

    [TestMethod]
    public void EarnFromBattle_AddsScaledAmount_BasedOnEnemySizeRatio()
    {
        _storage.Get("hero1", "war_spoils").Returns(100f);
        _service.EarnFromBattle("hero1", "empire_s", null, 1.5f);
        _storage.Received(1).Set("hero1", "war_spoils", 115f);
    }

    [TestMethod]
    public void EarnFromBattle_WorksViaCultureFallback()
    {
        _storage.Get("hero1", "war_spoils").Returns(100f);
        _service.EarnFromBattle("hero1", null, "mordor", 1.0f);
        _storage.Received(1).Set("hero1", "war_spoils", 110f);
    }

    [TestMethod]
    public void EarnFromBattle_ClampsRatio_ToMinHalf()
    {
        _storage.Get("hero1", "war_spoils").Returns(100f);
        _service.EarnFromBattle("hero1", "empire_s", null, 0.1f);
        _storage.Received(1).Set("hero1", "war_spoils", 105f);
    }

    [TestMethod]
    public void EarnFromBattle_ClampsRatio_ToMaxTwo()
    {
        _storage.Get("hero1", "war_spoils").Returns(100f);
        _service.EarnFromBattle("hero1", "empire_s", null, 5.0f);
        _storage.Received(1).Set("hero1", "war_spoils", 120f);
    }

    [TestMethod]
    public void EarnFromBattle_CapsAtResourceMax()
    {
        _storage.Get("hero1", "war_spoils").Returns(498f);
        _service.EarnFromBattle("hero1", "empire_s", null, 1.0f);
        _storage.Received(1).Set("hero1", "war_spoils", 500f);
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
        _storage.Get("hero1", "war_spoils").Returns(50f);
        _service.EarnFromRaid("hero1", "empire_s", null);
        _storage.Received(1).Set("hero1", "war_spoils", 58f);
    }

    [TestMethod]
    public void EarnFromSiege_AddsConfiguredAmount()
    {
        _storage.Get("hero1", "war_spoils").Returns(50f);
        _service.EarnFromSiege("hero1", "empire_s", null);
        _storage.Received(1).Set("hero1", "war_spoils", 65f);
    }

    [TestMethod]
    public void EarnFromPrisoners_AddsPerPrisonerTimesCount()
    {
        _storage.Get("hero1", "war_spoils").Returns(50f);
        _service.EarnFromPrisoners("hero1", "empire_s", null, 7);
        _storage.Received(1).Set("hero1", "war_spoils", 57f);
    }

    // ── Upgrade Affordability ──

    [TestMethod]
    public void CanAffordUpgrade_ReturnsFalse_WhenInsufficientResources()
    {
        var cost = new TroopResourceCostEntry("mordor_uruk_deathwarden", "war_spoils", 5, 0.3f);
        _config.GetTroopCost("mordor_uruk_deathwarden").Returns(cost);
        _storage.Get("hero1", "war_spoils").Returns(8f);

        Assert.IsFalse(_service.CanAffordUpgrade("hero1", "empire_s", null, "mordor_uruk_deathwarden", 2));
    }

    [TestMethod]
    public void CanAffordUpgrade_ReturnsTrue_WhenSufficientResources()
    {
        var cost = new TroopResourceCostEntry("mordor_uruk_deathwarden", "war_spoils", 5, 0.3f);
        _config.GetTroopCost("mordor_uruk_deathwarden").Returns(cost);
        _storage.Get("hero1", "war_spoils").Returns(15f);

        Assert.IsTrue(_service.CanAffordUpgrade("hero1", "empire_s", null, "mordor_uruk_deathwarden", 2));
    }

    [TestMethod]
    public void SpendForUpgrade_DeductsCorrectAmount()
    {
        var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", 4, 0.2f);
        _config.GetTroopCost("mordor_uruk_captain").Returns(cost);

        _service.SpendForUpgrade("hero1", "empire_s", null, "mordor_uruk_captain", 3);
        _storage.Received(1).Add("hero1", "war_spoils", -12f);
    }

    // ── Recruit Cost (elephant/spider volunteer gate) ──

    [TestMethod]
    public void ChargeRecruitCost_DeductsRecruitCostTimesCount()
    {
        var cost = new TroopResourceCostEntry("harad_elephant_rider", "war_drums", upgradeCost: 0, dailyUpkeep: 10f, recruitCost: 50);
        _config.GetTroopCost("harad_elephant_rider").Returns(cost);

        _service.ChargeRecruitCost("hero1", "empire_s", null, "harad_elephant_rider", 2);

        _storage.Received(1).Add("hero1", "war_spoils", -100f);
    }

    [TestMethod]
    public void ChargeRecruitCost_NoCostEntry_NoOp()
    {
        _config.GetTroopCost("plain_troop").Returns((TroopResourceCostEntry)null);

        _service.ChargeRecruitCost("hero1", "empire_s", null, "plain_troop", 1);

        _storage.DidNotReceive().Add(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>());
    }

    [TestMethod]
    public void ChargeRecruitCost_ZeroRecruitCost_NoOp()
    {
        // An upkeep-only entry (recruit_cost omitted) must not deduct on recruit.
        var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", upgradeCost: 4, dailyUpkeep: 0.2f, recruitCost: 0);
        _config.GetTroopCost("mordor_uruk_captain").Returns(cost);

        _service.ChargeRecruitCost("hero1", "empire_s", null, "mordor_uruk_captain", 3);

        _storage.DidNotReceive().Add(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>());
    }

    [TestMethod]
    public void ChargeRecruitCost_NoResolvedResource_NoOp()
    {
        var cost = new TroopResourceCostEntry("harad_elephant_rider", "war_drums", upgradeCost: 0, dailyUpkeep: 10f, recruitCost: 50);
        _config.GetTroopCost("harad_elephant_rider").Returns(cost);

        _service.ChargeRecruitCost("hero1", "unmapped_kingdom", null, "harad_elephant_rider", 1);

        _storage.DidNotReceive().Add(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>());
    }

    [TestMethod]
    public void CanAffordRecruit_BalanceEqualsCost_Allowed()
    {
        var cost = new TroopResourceCostEntry("harad_elephant_rider", "war_drums", upgradeCost: 0, dailyUpkeep: 10f, recruitCost: 50);
        _config.GetTroopCost("harad_elephant_rider").Returns(cost);
        _storage.Get("hero1", "war_spoils").Returns(50f);

        var result = _service.CanAffordRecruit("hero1", "empire_s", null,
            new List<RecruitCartEntry> { new("harad_elephant_rider", 1) });

        Assert.IsFalse(result.Blocked);
    }

    [TestMethod]
    public void CanAffordRecruit_BalanceBelowCost_Blocked()
    {
        var cost = new TroopResourceCostEntry("harad_elephant_rider", "war_drums", upgradeCost: 0, dailyUpkeep: 10f, recruitCost: 50);
        _config.GetTroopCost("harad_elephant_rider").Returns(cost);
        _storage.Get("hero1", "war_spoils").Returns(49f);

        var result = _service.CanAffordRecruit("hero1", "empire_s", null,
            new List<RecruitCartEntry> { new("harad_elephant_rider", 1) });

        Assert.IsTrue(result.Blocked);
        Assert.AreEqual(50, result.Required);
        Assert.AreEqual("War Spoils", result.ResourceDisplayName);
    }

    [TestMethod]
    public void CanAffordRecruit_SumsMultipleCartEntries()
    {
        var cost = new TroopResourceCostEntry("harad_elephant_rider", "war_drums", upgradeCost: 0, dailyUpkeep: 10f, recruitCost: 50);
        _config.GetTroopCost("harad_elephant_rider").Returns(cost);
        _storage.Get("hero1", "war_spoils").Returns(120f);

        var result = _service.CanAffordRecruit("hero1", "empire_s", null,
            new List<RecruitCartEntry> { new("harad_elephant_rider", 3) }); // 150 > 120

        Assert.IsTrue(result.Blocked);
        Assert.AreEqual(150, result.Required);
    }

    [TestMethod]
    public void CanAffordRecruit_CartTroopHasNoRecruitCost_Allowed()
    {
        _config.GetTroopCost("plain_troop").Returns((TroopResourceCostEntry)null);
        _storage.Get("hero1", "war_spoils").Returns(0f);

        var result = _service.CanAffordRecruit("hero1", "empire_s", null,
            new List<RecruitCartEntry> { new("plain_troop", 5) });

        Assert.IsFalse(result.Blocked);
    }

    [TestMethod]
    public void CanAffordRecruit_NoResolvedResource_Allowed()
    {
        var cost = new TroopResourceCostEntry("harad_elephant_rider", "war_drums", upgradeCost: 0, dailyUpkeep: 10f, recruitCost: 50);
        _config.GetTroopCost("harad_elephant_rider").Returns(cost);

        var result = _service.CanAffordRecruit("hero1", "unmapped_kingdom", null,
            new List<RecruitCartEntry> { new("harad_elephant_rider", 1) });

        Assert.IsFalse(result.Blocked);
    }

    [TestMethod]
    public void CanAffordRecruit_EmptyCart_Allowed()
    {
        var result = _service.CanAffordRecruit("hero1", "empire_s", null, new List<RecruitCartEntry>());
        Assert.IsFalse(result.Blocked);
    }

    // ── Merchant Purchase (Elite Emissary) ──
    // merchant_cost is a SEPARATE field from recruit_cost so the emissary never collides with the
    // volunteer gate. The charged resource is resolved from the SETTLEMENT OWNER's faction (the
    // kingdom/culture args), not the player's clan.

    [TestMethod]
    public void CanAffordMerchantPurchase_BalanceAboveCost_ReturnsTrue()
    {
        var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", upgradeCost: 4, dailyUpkeep: 0f, recruitCost: 0, merchantCost: 30);
        _config.GetTroopCost("mordor_uruk_captain").Returns(cost);
        _storage.Get("hero1", "war_spoils").Returns(100f);

        Assert.IsTrue(_service.CanAffordMerchantPurchase("hero1", "empire_s", null, "mordor_uruk_captain", 3)); // 90 ≤ 100
    }

    [TestMethod]
    public void CanAffordMerchantPurchase_BalanceBelowCost_ReturnsFalse()
    {
        var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", upgradeCost: 4, dailyUpkeep: 0f, recruitCost: 0, merchantCost: 30);
        _config.GetTroopCost("mordor_uruk_captain").Returns(cost);
        _storage.Get("hero1", "war_spoils").Returns(80f);

        Assert.IsFalse(_service.CanAffordMerchantPurchase("hero1", "empire_s", null, "mordor_uruk_captain", 3)); // 90 > 80
    }

    [TestMethod]
    public void CanAffordMerchantPurchase_BalanceEqualsCost_ReturnsTrue()
    {
        var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", upgradeCost: 4, dailyUpkeep: 0f, recruitCost: 0, merchantCost: 30);
        _config.GetTroopCost("mordor_uruk_captain").Returns(cost);
        _storage.Get("hero1", "war_spoils").Returns(90f);

        Assert.IsTrue(_service.CanAffordMerchantPurchase("hero1", "empire_s", null, "mordor_uruk_captain", 3)); // 90 == 90
    }

    [TestMethod]
    public void CanAffordMerchantPurchase_NoMerchantCost_AllowsByDefault()
    {
        // An upgrade/upkeep-only entry (merchant_cost omitted) is not an emissary offer; afford-allow
        // so the gate decision lives in the offer-list builder, not here.
        var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", upgradeCost: 4, dailyUpkeep: 0.2f);
        _config.GetTroopCost("mordor_uruk_captain").Returns(cost);
        _storage.Get("hero1", "war_spoils").Returns(0f);

        Assert.IsTrue(_service.CanAffordMerchantPurchase("hero1", "empire_s", null, "mordor_uruk_captain", 5));
    }

    [TestMethod]
    public void CanAffordMerchantPurchase_ZeroCount_AllowsByDefault()
    {
        var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", upgradeCost: 0, dailyUpkeep: 0f, recruitCost: 0, merchantCost: 30);
        _config.GetTroopCost("mordor_uruk_captain").Returns(cost);
        _storage.Get("hero1", "war_spoils").Returns(0f);

        Assert.IsTrue(_service.CanAffordMerchantPurchase("hero1", "empire_s", null, "mordor_uruk_captain", 0));
    }

    [TestMethod]
    public void ChargeMerchantPurchase_DeductsMerchantCostTimesCount_FromOwnerResource()
    {
        var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", upgradeCost: 4, dailyUpkeep: 0f, recruitCost: 0, merchantCost: 30);
        _config.GetTroopCost("mordor_uruk_captain").Returns(cost);

        _service.ChargeMerchantPurchase("hero1", "empire_s", null, "mordor_uruk_captain", 2);

        _storage.Received(1).Add("hero1", "war_spoils", -60f);
    }

    [TestMethod]
    public void ChargeMerchantPurchase_NoMerchantCost_NoOp()
    {
        // recruit_cost set but merchant_cost 0 — must NOT deduct (proves the two economies don't cross).
        var cost = new TroopResourceCostEntry("harad_elephant_rider", "war_drums", upgradeCost: 0, dailyUpkeep: 10f, recruitCost: 50, merchantCost: 0);
        _config.GetTroopCost("harad_elephant_rider").Returns(cost);

        _service.ChargeMerchantPurchase("hero1", "empire_s", null, "harad_elephant_rider", 2);

        _storage.DidNotReceive().Add(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>());
    }

    [TestMethod]
    public void ChargeMerchantPurchase_NoCostEntry_NoOp()
    {
        _config.GetTroopCost("plain_troop").Returns((TroopResourceCostEntry)null);

        _service.ChargeMerchantPurchase("hero1", "empire_s", null, "plain_troop", 1);

        _storage.DidNotReceive().Add(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>());
    }

    [TestMethod]
    public void ChargeMerchantPurchase_NoResolvedResource_NoOp()
    {
        var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", upgradeCost: 0, dailyUpkeep: 0f, recruitCost: 0, merchantCost: 30);
        _config.GetTroopCost("mordor_uruk_captain").Returns(cost);

        _service.ChargeMerchantPurchase("hero1", "unmapped_kingdom", null, "mordor_uruk_captain", 1);

        _storage.DidNotReceive().Add(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>());
    }

    [TestMethod]
    public void ChargeMerchantPurchase_ZeroCount_NoOp()
    {
        var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", upgradeCost: 0, dailyUpkeep: 0f, recruitCost: 0, merchantCost: 30);
        _config.GetTroopCost("mordor_uruk_captain").Returns(cost);

        _service.ChargeMerchantPurchase("hero1", "empire_s", null, "mordor_uruk_captain", 0);

        _storage.DidNotReceive().Add(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>());
    }

    [TestMethod]
    public void RecruitVsMerchant_SameTroopWithBothCosts_ChargeIndependentFields()
    {
        // The headline "never double-charged" invariant, load-bearing only for a troop that is BOTH a
        // recruitable volunteer AND an emissary offer (harad_elephant_rider / taom_spider_creature):
        // the volunteer path charges recruit_cost, the emissary path charges merchant_cost — never the
        // other's field. Deep-review 2026-06-25 (completeness critic gap #5).
        var cost = new TroopResourceCostEntry("harad_elephant_rider", "war_drums", upgradeCost: 0, dailyUpkeep: 10f, recruitCost: 50, merchantCost: 70);
        _config.GetTroopCost("harad_elephant_rider").Returns(cost);

        _service.ChargeRecruitCost("hero1", "empire_s", null, "harad_elephant_rider", 1);
        _storage.Received(1).Add("hero1", "war_spoils", -50f);   // volunteer path → recruit_cost

        _service.ChargeMerchantPurchase("hero1", "empire_s", null, "harad_elephant_rider", 1);
        _storage.Received(1).Add("hero1", "war_spoils", -70f);   // emissary path → merchant_cost
    }

    // ── Daily Tick ──

    [TestMethod]
    public void ApplyDailyTick_EarningExceedsUpkeep_AddsCapped()
    {
        _storage.Get("hero1", "war_spoils").Returns(100f);
        _service.ApplyDailyTick("hero1", "empire_s", null, 4, new List<TroopUpkeepInfo>());
        _storage.Received(1).Set("hero1", "war_spoils", 102f);
    }

    [TestMethod]
    public void ApplyDailyTick_UpkeepExceedsEarning_SubtractsFromStorage()
    {
        _storage.Get("hero1", "war_spoils").Returns(100f);
        var upkeepCost = new TroopResourceCostEntry("mordor_uruk_deathwarden", "war_spoils", 5, 0.3f);
        _config.GetTroopCost("mordor_uruk_deathwarden").Returns(upkeepCost);
        var troops = new List<TroopUpkeepInfo> { new("mordor_uruk_deathwarden", 20) };

        _service.ApplyDailyTick("hero1", "empire_s", null, 0, troops);
        _storage.Received(1).Add("hero1", "war_spoils", -6f);
    }

    [TestMethod]
    public void GetDailyEarning_ReturnsPerTownTimesCount()
    {
        Assert.AreEqual(1.5f, _service.GetDailyEarning("empire_s", null, 3));
    }

    // ── Projected Daily Net (deficit warning) ──

    [TestMethod]
    public void GetProjectedDailyNet_EarningExceedsUpkeep_ReturnsPositive()
    {
        // 4 towns * 0.5 = 2.0 earning, no upkeep troops → net +2.0
        var net = _service.GetProjectedDailyNet("hero1", "empire_s", null, 4, new List<TroopUpkeepInfo>());
        Assert.AreEqual(2.0f, net, 0.001f);
    }

    [TestMethod]
    public void GetProjectedDailyNet_UpkeepExceedsEarning_ReturnsNegative()
    {
        // 0 towns → 0 earning; 20 troops * 0.3 = 6.0 upkeep → net -6.0
        var upkeepCost = new TroopResourceCostEntry("mordor_uruk_deathwarden", "war_spoils", 5, 0.3f);
        _config.GetTroopCost("mordor_uruk_deathwarden").Returns(upkeepCost);
        var troops = new List<TroopUpkeepInfo> { new("mordor_uruk_deathwarden", 20) };

        var net = _service.GetProjectedDailyNet("hero1", "empire_s", null, 0, troops);

        Assert.AreEqual(-6.0f, net, 0.001f);
    }

    [TestMethod]
    public void GetProjectedDailyNet_NoResource_ReturnsZero()
    {
        var net = _service.GetProjectedDailyNet("hero1", "nonexistent_kingdom", null, 4, new List<TroopUpkeepInfo>());
        Assert.AreEqual(0f, net, 0.001f);
    }

    [TestMethod]
    public void GetProjectedDailyNet_AppliesCareerGainAndUpkeepModifiers()
    {
        // Mirrors ApplyDailyTick math: earning gets +SpecialResourceGain, upkeep gets SpecialResourceUpkeepModifier.
        // Earning: 4 * 0.5 = 2.0, +20% = 2.4. Upkeep: 10 * 0.3 = 3.0, -50% = 1.5. Net = 2.4 - 1.5 = 0.9.
        _passiveService.GetPassiveMagnitude("hero1", PassiveEffectType.SpecialResourceGain).Returns(0.2f);
        _passiveService.GetPassiveMagnitude("hero1", PassiveEffectType.SpecialResourceUpkeepModifier).Returns(-0.5f);
        var upkeepCost = new TroopResourceCostEntry("mordor_uruk_deathwarden", "war_spoils", 5, 0.3f);
        _config.GetTroopCost("mordor_uruk_deathwarden").Returns(upkeepCost);
        var troops = new List<TroopUpkeepInfo> { new("mordor_uruk_deathwarden", 10) };

        var net = _service.GetProjectedDailyNet("hero1", "empire_s", null, 4, troops);

        Assert.AreEqual(0.9f, net, 0.001f);
    }

    // ── Pending Transaction ──

    [TestMethod]
    public void QueueUpgradeSpend_DoesNotMutateStorage()
    {
        var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", 4, 0.2f);
        _config.GetTroopCost("mordor_uruk_captain").Returns(cost);

        _service.BeginPartyScreenSession();
        _service.QueueUpgradeSpend("hero1", "mordor_uruk_captain", 3);

        _storage.DidNotReceive().Add(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>());
        _storage.DidNotReceive().Set(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>());
    }

    [TestMethod]
    public void GetAvailableAfterPending_SubtractsPendingFromStorage()
    {
        var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", 4, 0.2f);
        _config.GetTroopCost("mordor_uruk_captain").Returns(cost);
        _storage.Get("hero1", "war_spoils").Returns(100f);

        _service.BeginPartyScreenSession();
        _service.QueueUpgradeSpend("hero1", "mordor_uruk_captain", 3);

        Assert.AreEqual(88f, _service.GetAvailableAfterPending("hero1", "empire_s", null));
    }

    [TestMethod]
    public void CommitSession_AppliesPendingToStorage()
    {
        var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", 4, 0.2f);
        _config.GetTroopCost("mordor_uruk_captain").Returns(cost);

        _service.BeginPartyScreenSession();
        _service.QueueUpgradeSpend("hero1", "mordor_uruk_captain", 3);
        _service.CommitSession("hero1", "empire_s", null);

        _storage.Received(1).Add("hero1", "war_spoils", -12f);
    }

    [TestMethod]
    public void CancelSession_DiscardsAndNeverMutatesStorage()
    {
        var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", 4, 0.2f);
        _config.GetTroopCost("mordor_uruk_captain").Returns(cost);

        _service.BeginPartyScreenSession();
        _service.QueueUpgradeSpend("hero1", "mordor_uruk_captain", 3);
        _service.CancelSession();

        _storage.DidNotReceive().Add(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>());
    }

    [TestMethod]
    public void ClampUpgradeCount_LimitsByAvailableMinusPending()
    {
        var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", 4, 0.2f);
        _config.GetTroopCost("mordor_uruk_captain").Returns(cost);
        _storage.Get("hero1", "war_spoils").Returns(10f);

        _service.BeginPartyScreenSession();
        Assert.AreEqual(2, _service.ClampUpgradeCount("hero1", "empire_s", null, "mordor_uruk_captain", 5));
    }

    [TestMethod]
    public void QueueUpgradeSpend_WithPassiveDiscount_DebitsEffectiveCost()
    {
        // Regression test for #174 / #194. Pre-fix, ClampUpgradeCount + CanAffordUpgrade + SpendForUpgrade
        // all called GetEffectiveUpgradeCost (discounted), but QueueUpgradeSpend used the bare base cost.
        // A career with -30% SpecialResourceUpgradeCostModifier would let the player queue upgrades at
        // the discounted gate, then get debited the full base price at CommitSession — silently
        // overpaying by the discount percentage.
        //
        // The fix threads heroId through QueueUpgradeSpend so the queued + committed amount matches
        // the gate's effective per-unit cost. Setup:
        //   - Base 10 per unit, -30% career discount -> effective 7 per unit
        //   - Queue 1 unit -> pending = 7 (pre-fix this was 10)
        //   - CommitSession -> storage.Add(..., -7) (pre-fix: -10)
        var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", upgradeCost: 10, dailyUpkeep: 0.2f);
        _config.GetTroopCost("mordor_uruk_captain").Returns(cost);
        _passiveService
            .GetPassiveMagnitude("hero1", PassiveEffectType.SpecialResourceUpgradeCostModifier)
            .Returns(-0.30f);

        _service.BeginPartyScreenSession();
        _service.QueueUpgradeSpend("hero1", "mordor_uruk_captain", 1);
        _service.CommitSession("hero1", "empire_s", null);

        _storage.Received(1).Add("hero1", "war_spoils", -7f);
    }

    [TestMethod]
    public void QueueUpgradeSpend_NoCareerDiscount_DebitsBaseCost()
    {
        // Negative-case partner to QueueUpgradeSpend_WithPassiveDiscount_DebitsEffectiveCost: confirms
        // the fix doesn't accidentally change behavior when no discount is active (passive = 0).
        var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", upgradeCost: 10, dailyUpkeep: 0.2f);
        _config.GetTroopCost("mordor_uruk_captain").Returns(cost);
        // No _passiveService.Returns(...) configured — Substitute.For default-returns 0f.

        _service.BeginPartyScreenSession();
        _service.QueueUpgradeSpend("hero1", "mordor_uruk_captain", 3);
        _service.CommitSession("hero1", "empire_s", null);

        _storage.Received(1).Add("hero1", "war_spoils", -30f);
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
        _storage.Received(1).Set("hero1", "war_spoils", 30f);
    }

    [TestMethod]
    public void InitializeHero_WorksViaCulture()
    {
        _service.InitializeHero("hero1", null, "mordor");
        _storage.Received(1).Set("hero1", "war_spoils", 30f);
    }

    [TestMethod]
    public void GetCurrentAmount_ReturnsZero_WhenNoResourceResolved()
    {
        _config.GetByKingdomId("empire_w").Returns((SpecialResource)null);
        _config.GetByCultureId("gondor").Returns((SpecialResource)null);
        Assert.AreEqual(0f, _service.GetCurrentAmount("hero1", "empire_w", "gondor"));
    }

    // ── Desertion ──

    [TestMethod]
    public void CalculateDesertion_BalanceAboveZero_ReturnsEmpty()
    {
        _storage.Get("hero1", "war_spoils").Returns(10f);
        var troops = new List<TroopUpkeepInfo> { new("mordor_uruk_darkblade", 20) };

        var result = _service.CalculateDesertion("hero1", "empire_s", null, troops);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void CalculateDesertion_BalanceZero_Deserts10Percent()
    {
        _storage.Get("hero1", "war_spoils").Returns(0f);
        var troops = new List<TroopUpkeepInfo> { new("mordor_uruk_darkblade", 20) };

        var result = _service.CalculateDesertion("hero1", "empire_s", null, troops);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("mordor_uruk_darkblade", result[0].TroopId);
        Assert.AreEqual(2, result[0].DesertCount); // 10% of 20 = 2
    }

    [TestMethod]
    public void CalculateDesertion_BalanceZero_MinimumOnePerType()
    {
        _storage.Get("hero1", "war_spoils").Returns(0f);
        var troops = new List<TroopUpkeepInfo> { new("mordor_uruk_darkblade", 3) };

        var result = _service.CalculateDesertion("hero1", "empire_s", null, troops);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(1, result[0].DesertCount); // 10% of 3 = 0.3 → min 1
    }

    [TestMethod]
    public void CalculateDesertion_NoTroops_ReturnsEmpty()
    {
        _storage.Get("hero1", "war_spoils").Returns(0f);
        var troops = new List<TroopUpkeepInfo>();

        var result = _service.CalculateDesertion("hero1", "empire_s", null, troops);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void CalculateDesertion_NoResource_ReturnsEmpty()
    {
        _config.GetByKingdomId("empire_w").Returns((SpecialResource)null);
        _config.GetByCultureId("gondor").Returns((SpecialResource)null);
        var troops = new List<TroopUpkeepInfo> { new("gondor_knight", 10) };

        var result = _service.CalculateDesertion("hero1", "empire_w", "gondor", troops);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void CalculateDesertion_MultipleTroopTypes_DesertsEach()
    {
        _storage.Get("hero1", "war_spoils").Returns(0f);
        var troops = new List<TroopUpkeepInfo>
        {
            new("mordor_uruk_darkblade", 10),
            new("mordor_uruk_deathwarden", 5)
        };

        var result = _service.CalculateDesertion("hero1", "empire_s", null, troops);

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual(1, result[0].DesertCount); // 10% of 10 = 1
        Assert.AreEqual(1, result[1].DesertCount); // 10% of 5 = 0.5 → min 1
    }

    // ── Career Passive Integration ──

    [TestMethod]
    public void ApplyDailyTick_SpecialResourceGain_ScalesEarning()
    {
        // 0.2 = 20% bonus to resource gain
        _passiveService.GetPassiveMagnitude("hero1", PassiveEffectType.SpecialResourceGain).Returns(0.2f);
        _storage.Get("hero1", "war_spoils").Returns(100f);

        _service.ApplyDailyTick("hero1", "empire_s", null, 4, new List<TroopUpkeepInfo>());

        // Base earning = 0.5 * 4 = 2.0, with 20% bonus = 2.4
        _storage.Received(1).Set("hero1", "war_spoils", 102.4f);
    }

    [TestMethod]
    public void ApplyDailyTick_NoCareerPassive_EarningUnchanged()
    {
        _passiveService.GetPassiveMagnitude("hero1", PassiveEffectType.SpecialResourceGain).Returns(0f);
        _storage.Get("hero1", "war_spoils").Returns(100f);

        _service.ApplyDailyTick("hero1", "empire_s", null, 4, new List<TroopUpkeepInfo>());

        // Base earning = 0.5 * 4 = 2.0, no bonus
        _storage.Received(1).Set("hero1", "war_spoils", 102f);
    }

    [TestMethod]
    public void GetDailyUpkeep_SpecialResourceUpkeepModifier_ReducesUpkeep()
    {
        // -0.25 = 25% upkeep reduction
        _passiveService.GetPassiveMagnitude("hero1", PassiveEffectType.SpecialResourceUpkeepModifier).Returns(-0.25f);
        var upkeepCost = new TroopResourceCostEntry("mordor_uruk_deathwarden", "war_spoils", 5, 0.3f);
        _config.GetTroopCost("mordor_uruk_deathwarden").Returns(upkeepCost);
        var troops = new List<TroopUpkeepInfo> { new("mordor_uruk_deathwarden", 10) };

        var result = _service.GetDailyUpkeep(troops, "hero1");

        // Base upkeep = 0.3 * 10 = 3.0, with -25% modifier = 2.25
        Assert.AreEqual(2.25f, result, 0.001f);
    }

    [TestMethod]
    public void GetDailyUpkeep_NoCareerPassive_UpkeepUnchanged()
    {
        _passiveService.GetPassiveMagnitude("hero1", PassiveEffectType.SpecialResourceUpkeepModifier).Returns(0f);
        var upkeepCost = new TroopResourceCostEntry("mordor_uruk_deathwarden", "war_spoils", 5, 0.3f);
        _config.GetTroopCost("mordor_uruk_deathwarden").Returns(upkeepCost);
        var troops = new List<TroopUpkeepInfo> { new("mordor_uruk_deathwarden", 10) };

        var result = _service.GetDailyUpkeep(troops, "hero1");

        Assert.AreEqual(3.0f, result, 0.001f);
    }

    [TestMethod]
    public void GetDailyUpkeep_NullHeroId_UpkeepUnmodified()
    {
        var upkeepCost = new TroopResourceCostEntry("mordor_uruk_deathwarden", "war_spoils", 5, 0.3f);
        _config.GetTroopCost("mordor_uruk_deathwarden").Returns(upkeepCost);
        var troops = new List<TroopUpkeepInfo> { new("mordor_uruk_deathwarden", 10) };

        var result = _service.GetDailyUpkeep(troops, null);

        Assert.AreEqual(3.0f, result, 0.001f);
    }

    [TestMethod]
    public void SpendForUpgrade_SpecialResourceUpgradeCostModifier_ReducesCost()
    {
        // -0.3 = 30% cost reduction
        _passiveService.GetPassiveMagnitude("hero1", PassiveEffectType.SpecialResourceUpgradeCostModifier).Returns(-0.3f);
        var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", 10, 0.2f);
        _config.GetTroopCost("mordor_uruk_captain").Returns(cost);

        _service.SpendForUpgrade("hero1", "empire_s", null, "mordor_uruk_captain", 2);

        // Base cost = 10 * 2 = 20, with -30% modifier = 14
        _storage.Received(1).Add("hero1", "war_spoils", -14f);
    }

    [TestMethod]
    public void ClampUpgradeCount_SpecialResourceUpgradeCostModifier_AllowsMore()
    {
        // -0.5 = 50% cost reduction
        _passiveService.GetPassiveMagnitude("hero1", PassiveEffectType.SpecialResourceUpgradeCostModifier).Returns(-0.5f);
        var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", 4, 0.2f);
        _config.GetTroopCost("mordor_uruk_captain").Returns(cost);
        _storage.Get("hero1", "war_spoils").Returns(10f);

        _service.BeginPartyScreenSession();
        // At cost 4 with -50% = effective cost 2, available 10 → can afford 5
        Assert.AreEqual(5, _service.ClampUpgradeCount("hero1", "empire_s", null, "mordor_uruk_captain", 5));
    }

    [TestMethod]
    public void SpendForUpgrade_NoCareerPassive_CostUnchanged()
    {
        _passiveService.GetPassiveMagnitude("hero1", PassiveEffectType.SpecialResourceUpgradeCostModifier).Returns(0f);
        var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", 4, 0.2f);
        _config.GetTroopCost("mordor_uruk_captain").Returns(cost);

        _service.SpendForUpgrade("hero1", "empire_s", null, "mordor_uruk_captain", 3);

        _storage.Received(1).Add("hero1", "war_spoils", -12f);
    }

    [TestMethod]
    public void ApplyDailyTick_UpkeepModifier_AffectsNetCalculation()
    {
        // Earning: 4 towns * 0.5 = 2.0 (no gain modifier)
        // Upkeep: 10 troops * 0.3 = 3.0, with -50% modifier = 1.5
        // Net: 2.0 - 1.5 = 0.5 (positive, should AddCapped)
        _passiveService.GetPassiveMagnitude("hero1", PassiveEffectType.SpecialResourceGain).Returns(0f);
        _passiveService.GetPassiveMagnitude("hero1", PassiveEffectType.SpecialResourceUpkeepModifier).Returns(-0.5f);
        _storage.Get("hero1", "war_spoils").Returns(100f);
        var upkeepCost = new TroopResourceCostEntry("mordor_uruk_deathwarden", "war_spoils", 5, 0.3f);
        _config.GetTroopCost("mordor_uruk_deathwarden").Returns(upkeepCost);
        var troops = new List<TroopUpkeepInfo> { new("mordor_uruk_deathwarden", 10) };

        _service.ApplyDailyTick("hero1", "empire_s", null, 4, troops);

        // Net = 2.0 - 1.5 = 0.5 → 100 + 0.5 = 100.5
        _storage.Received(1).Set("hero1", "war_spoils", 100.5f);
    }

    // ── ResetSessionState (Phase 9b deferred #133 P2 R1) ──
    //
    // Service is registered as Reuse.Singleton — its private state (_loggedResolveKeys,
    // _pendingSpend, _inSession) survives across new-campaign-in-same-process boundaries.
    // ResetSessionState wipes that state so a second campaign doesn't inherit a stale
    // _inSession=true (which would let an orphaned CommitSession deduct from the new hero)
    // or a stale _pendingSpend (which would be applied at the next CommitSession).

    [TestMethod]
    public void ResetSessionState_ClearsPendingSpend()
    {
        var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", 4, 0.2f);
        _config.GetTroopCost("mordor_uruk_captain").Returns(cost);
        _storage.Get("hero1", "war_spoils").Returns(100f);

        _service.BeginPartyScreenSession();
        _service.QueueUpgradeSpend("hero1", "mordor_uruk_captain", 3);
        // Pre-reset: pending = 12 → GetAvailableAfterPending = 100 - 12 = 88.
        Assert.AreEqual(88f, _service.GetAvailableAfterPending("hero1", "empire_s", null));

        _service.ResetSessionState();

        // After reset: pending cleared → GetAvailableAfterPending == raw storage (100).
        Assert.AreEqual(100f, _service.GetAvailableAfterPending("hero1", "empire_s", null));
    }

    [TestMethod]
    public void ResetSessionState_ClearsInSession_CommitSessionBecomesNoOp()
    {
        var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", 4, 0.2f);
        _config.GetTroopCost("mordor_uruk_captain").Returns(cost);

        _service.BeginPartyScreenSession();
        _service.QueueUpgradeSpend("hero1", "mordor_uruk_captain", 3);

        _service.ResetSessionState();

        // After reset _inSession==false → CommitSession early-returns, no Add.
        _service.CommitSession("hero1", "empire_s", null);
        _storage.DidNotReceive().Add(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>());
    }

    [TestMethod]
    public void ResetSessionState_ClearsLoggedResolveKeys_ReResolutionLogsAgain()
    {
        // First resolve logs once, second is deduped silent.
        _service.ResolveResource("empire_s", null);
        _logger.ClearReceivedCalls();
        _service.ResolveResource("empire_s", null);
        _logger.DidNotReceive().LogDebug(Arg.Any<string>());

        // After reset, the dedupe key set is cleared — same call logs again.
        _service.ResetSessionState();
        _logger.ClearReceivedCalls();
        _service.ResolveResource("empire_s", null);
        _logger.Received(1).LogDebug(Arg.Is<string>(s => s.Contains("via kingdom 'empire_s'")));
    }
}
