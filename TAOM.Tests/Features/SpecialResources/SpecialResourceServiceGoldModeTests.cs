using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TAOM.Core.Logging;
using TAOM.Features;
using TAOM.Features.SpecialResources;
using TAOM.Features.SpecialResources.Domain;

namespace TAOM.Tests.Features.SpecialResources;

/// <summary>
/// #1109: with "Use Special Resources" off the elite troops' resource costs are paid in gold at
/// the MCM rate. Every method gates on the main party / main hero and on the toggle; the toggle
/// being on (or the gold settings being absent) must leave every number exactly as vanilla gave it.
/// </summary>
[TestClass]
public class SpecialResourceServiceGoldModeTests
{
    private ISpecialResourceConfigProvider _config;

    [TestInitialize]
    public void Setup()
    {
        _config = Substitute.For<ISpecialResourceConfigProvider>();
        _config.GetTroopCost("uruk_a").Returns(new TroopResourceCostEntry("uruk_a", "war_spoils", upgradeCost: 3, dailyUpkeep: 0.5f, recruitCost: 4));
        _config.GetTroopCost("uruk_b").Returns(new TroopResourceCostEntry("uruk_b", "war_spoils", upgradeCost: 0, dailyUpkeep: 2f));
        _config.GetTroopCost("uruk_nan").Returns(new TroopResourceCostEntry("uruk_nan", "war_spoils", upgradeCost: 0, dailyUpkeep: float.NaN));
    }

    private SpecialResourceService Service(bool useResources, int rate = 100, bool withSettings = true)
    {
        var settings = withSettings
            ? new SpecialResourceGoldSettings(new TaomSettings { UseSpecialResources = useResources, GoldPerResourcePoint = rate })
            : null;
        return new SpecialResourceService(_config, Substitute.For<ISpecialResourceStorageService>(), Substitute.For<IModLogger>(), null, settings);
    }

    private static List<TroopUpkeepInfo> Roster() => new()
    {
        new TroopUpkeepInfo("uruk_a", 4),
        new TroopUpkeepInfo("uruk_b", 1),
    };

    // ── IsGoldMode / rate ──

    [TestMethod]
    public void IsGoldMode_NoGoldSettings_False()
    {
        var service = Service(useResources: false, withSettings: false);

        Assert.IsFalse(service.IsGoldMode);
        Assert.AreEqual(100, service.GoldPerResourcePoint);
    }

    [TestMethod]
    public void IsGoldMode_McmOn_False() => Assert.IsFalse(Service(useResources: true).IsGoldMode);

    [TestMethod]
    public void IsGoldMode_McmOff_True()
    {
        var service = Service(useResources: false, rate: 250);

        Assert.IsTrue(service.IsGoldMode);
        Assert.AreEqual(250, service.GoldPerResourcePoint);
    }

    // ── Upkeep ──

    [TestMethod]
    public void ApplyGoldUpkeepSurcharge_MainPartyGoldMode_AddsUpkeepTimesCountTimesRate()
    {
        var wage = new ExplainedNumber(1000f);

        Service(useResources: false).ApplyGoldUpkeepSurcharge(true, Roster(), ref wage);

        Assert.AreEqual(1400f, wage.ResultNumber, 0.01f); // (0.5 x 4 + 2 x 1) points x 100 gold
    }

    [TestMethod]
    public void ApplyGoldUpkeepSurcharge_NotMainParty_Unchanged()
    {
        var wage = new ExplainedNumber(1000f);

        Service(useResources: false).ApplyGoldUpkeepSurcharge(false, Roster(), ref wage);

        Assert.AreEqual(1000f, wage.ResultNumber, 0.01f);
    }

    [TestMethod]
    public void ApplyGoldUpkeepSurcharge_ResourceMode_NeverEnumeratesRoster()
    {
        var wage = new ExplainedNumber(1000f);

        Service(useResources: true).ApplyGoldUpkeepSurcharge(true, ThrowingRoster(), ref wage);
        Service(useResources: true, withSettings: false).ApplyGoldUpkeepSurcharge(true, ThrowingRoster(), ref wage);

        Assert.AreEqual(1000f, wage.ResultNumber, 0.01f);
    }

    [TestMethod]
    public void ApplyGoldUpkeepSurcharge_NotMainParty_NeverEnumeratesRoster()
    {
        var wage = new ExplainedNumber(1000f);

        Service(useResources: false).ApplyGoldUpkeepSurcharge(false, ThrowingRoster(), ref wage);

        Assert.AreEqual(1000f, wage.ResultNumber, 0.01f);
    }

    [TestMethod]
    public void ApplyGoldUpkeepSurcharge_NaNUpkeepRow_Skipped()
    {
        var wage = new ExplainedNumber(1000f);
        var roster = new List<TroopUpkeepInfo> { new("uruk_nan", 3), new("uruk_b", 1), new("unknown", 9), null, new("uruk_a", 0) };

        Service(useResources: false).ApplyGoldUpkeepSurcharge(true, roster, ref wage);

        Assert.AreEqual(1200f, wage.ResultNumber, 0.01f); // only uruk_b: 2 points x 100
    }

    [TestMethod]
    public void ApplyGoldUpkeepSurcharge_NullRoster_Unchanged()
    {
        var wage = new ExplainedNumber(1000f);

        Service(useResources: false).ApplyGoldUpkeepSurcharge(true, null, ref wage);

        Assert.AreEqual(1000f, wage.ResultNumber, 0.01f);
    }

    [TestMethod]
    public void ApplyGoldUpkeepSurcharge_WithWageFactor_SurchargeIsScaled()
    {
        // The accepted design (#1109): the surcharge is an Add, so every factor that scales vanilla
        // gold (Frugal, the wage feats, the career TroopWages passive) scales it too. A flat
        // surcharge would show 1000 x 1.5 + 400 = 1900.
        var wage = new ExplainedNumber(1000f);
        wage.AddFactor(0.5f);

        Service(useResources: false).ApplyGoldUpkeepSurcharge(true, Roster(), ref wage);

        Assert.AreEqual(2100f, wage.ResultNumber, 0.01f); // (1000 + 400) x 1.5
    }

    // ── Upgrade ──

    [TestMethod]
    public void ApplyGoldUpgradeSurcharge_MainParty_AddsTargetUpgradeCostTimesRate()
    {
        var cost = new ExplainedNumber(200f);

        Service(useResources: false, rate: 50).ApplyGoldUpgradeSurcharge(true, "uruk_a", ref cost);

        Assert.AreEqual(350f, cost.ResultNumber, 0.01f); // 3 points x 50
    }

    [TestMethod]
    public void ApplyGoldUpgradeSurcharge_NotMainParty_Unchanged()
    {
        var cost = new ExplainedNumber(200f);

        Service(useResources: false).ApplyGoldUpgradeSurcharge(false, "uruk_a", ref cost);

        Assert.AreEqual(200f, cost.ResultNumber, 0.01f);
    }

    [TestMethod]
    public void ApplyGoldUpgradeSurcharge_UnknownTroop_Unchanged()
    {
        var cost = new ExplainedNumber(200f);

        Service(useResources: false).ApplyGoldUpgradeSurcharge(true, "nobody", ref cost);
        Service(useResources: false).ApplyGoldUpgradeSurcharge(true, null, ref cost);
        Service(useResources: false).ApplyGoldUpgradeSurcharge(true, "uruk_b", ref cost); // upgrade_cost 0

        Assert.AreEqual(200f, cost.ResultNumber, 0.01f);
    }

    [TestMethod]
    public void ApplyGoldUpgradeSurcharge_ResourceMode_Unchanged()
    {
        var cost = new ExplainedNumber(200f);

        Service(useResources: true).ApplyGoldUpgradeSurcharge(true, "uruk_a", ref cost);

        Assert.AreEqual(200f, cost.ResultNumber, 0.01f);
    }

    // ── Recruit ──

    [TestMethod]
    public void GetGoldRecruitSurcharge_MainHeroBuyer_ReturnsRecruitCostTimesRate() =>
        Assert.AreEqual(400, Service(useResources: false).GetGoldRecruitSurcharge(true, "uruk_a"));

    [TestMethod]
    public void GetGoldRecruitSurcharge_OtherBuyer_Zero() =>
        Assert.AreEqual(0, Service(useResources: false).GetGoldRecruitSurcharge(false, "uruk_a"));

    [TestMethod]
    public void GetGoldRecruitSurcharge_ResourceMode_Zero() =>
        Assert.AreEqual(0, Service(useResources: true).GetGoldRecruitSurcharge(true, "uruk_a"));

    [TestMethod]
    public void GetGoldRecruitSurcharge_NoRecruitCostOrUnknownTroop_Zero()
    {
        var service = Service(useResources: false);

        Assert.AreEqual(0, service.GetGoldRecruitSurcharge(true, "uruk_b"));
        Assert.AreEqual(0, service.GetGoldRecruitSurcharge(true, "nobody"));
        Assert.AreEqual(0, service.GetGoldRecruitSurcharge(true, null));
    }

    // ── Prisoner recruits (party screen): one charge for every recruited prisoner unit ──

    [TestMethod]
    public void GetGoldPrisonerRecruitCharge_GoldMode_SumsRecruitCostPerUnitTimesRate() =>
        // uruk_a x3 at recruit_cost 4 = 12 points, uruk_b carries none, an unknown id none.
        Assert.AreEqual(1200, Service(useResources: false).GetGoldPrisonerRecruitCharge(new[] { "uruk_a", "uruk_b", "uruk_a", "nobody", null, "uruk_a" }));

    [TestMethod]
    public void GetGoldPrisonerRecruitCharge_ResourceMode_Zero() =>
        Assert.AreEqual(0, Service(useResources: true).GetGoldPrisonerRecruitCharge(new[] { "uruk_a" }));

    [TestMethod]
    public void GetGoldPrisonerRecruitCharge_NullOrEmpty_Zero()
    {
        var service = Service(useResources: false);

        Assert.AreEqual(0, service.GetGoldPrisonerRecruitCharge(null));
        Assert.AreEqual(0, service.GetGoldPrisonerRecruitCharge(new string[0]));
    }

    [TestMethod]
    public void GetGoldPrisonerRecruitCharge_HugeRoster_SaturatesAtMaxGold()
    {
        _config.GetTroopCost("mumak").Returns(new TroopResourceCostEntry("mumak", "war_drums", upgradeCost: 0, dailyUpkeep: 0f, recruitCost: 2000));
        var ids = new string[6000];
        for (var i = 0; i < ids.Length; i++) ids[i] = "mumak";

        // 6000 x 2000 x 1000 is 1.2e10, past int.MaxValue: the sum must saturate, never wrap.
        Assert.AreEqual(SpecialResourceGoldPrice.MaxGold, Service(useResources: false, rate: 1000).GetGoldPrisonerRecruitCharge(ids));
    }

    private static IEnumerable<TroopUpkeepInfo> ThrowingRoster()
    {
        throw new InvalidOperationException("the roster must not be walked");
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }
}
