using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.SpecialResources;
using TAOM.Features.SpecialResources.Domain;
using TAOM.Features.SpecialResources.Hooks;

namespace TAOM.Tests.Features.SpecialResources;

/// <summary>
/// #1109: the two hook seams stand down every Patch26 (party-screen upgrade gate) and Patch51
/// (volunteer recruit gate) patch at once, because those patches read nothing but these hooks.
/// </summary>
[TestClass]
public class SpecialResourceGoldModeHookTests
{
    private static readonly SpecialResource War = new(
        id: "war_spoils", kingdomIds: new[] { "mordor" }, cultureIds: new[] { "mordor" },
        displayName: "War Spoils", iconSpriteName: "icon", cap: 500f, startingAmount: 30f, dailyPerTown: 0.5f,
        perBattleVictoryBase: 10f, perRaid: 8f, perSiegeVictory: 15f, perPrisoner: 1f);

    private ISpecialResourceService _service;
    private ISpecialResourceConfigProvider _config;

    [TestInitialize]
    public void Setup()
    {
        _service = Substitute.For<ISpecialResourceService>();
        _config = Substitute.For<ISpecialResourceConfigProvider>();
        _config.GetTroopCost("uruk_a").Returns(new TroopResourceCostEntry("uruk_a", "war_spoils", upgradeCost: 3, dailyUpkeep: 0f));
        _service.ResolveResource("mordor", "mordor").Returns(War);
    }

    // ── PartyUpgradeResourceCheckHook ──

    [TestMethod]
    public void GetUpgradeCost_GoldMode_ReturnsZero()
    {
        _service.IsGoldMode.Returns(true);

        Assert.AreEqual(0, new PartyUpgradeResourceCheckHook(_service, _config).GetUpgradeCost("uruk_a"));
    }

    [TestMethod]
    public void GetResourceDisplayName_GoldMode_ReturnsNull()
    {
        _service.IsGoldMode.Returns(true);

        Assert.IsNull(new PartyUpgradeResourceCheckHook(_service, _config).GetResourceDisplayName("mordor", "mordor"));
    }

    [TestMethod]
    public void GetUpgradeCost_ResourceMode_ReturnsConfigCost() =>
        Assert.AreEqual(3, new PartyUpgradeResourceCheckHook(_service, _config).GetUpgradeCost("uruk_a"));

    [TestMethod]
    public void GetResourceDisplayName_ResourceMode_ReturnsResourceName() =>
        Assert.AreEqual("War Spoils", new PartyUpgradeResourceCheckHook(_service, _config).GetResourceDisplayName("mordor", "mordor"));

    // ── RecruitmentResourceGateHook ──

    [TestMethod]
    public void EvaluateCart_GoldMode_AllowedAndCanAffordRecruitNotCalled()
    {
        _service.IsGoldMode.Returns(true);
        var cart = new List<RecruitCartEntry> { new("uruk_a", 2) };

        var result = new RecruitmentResourceGateHook(_service).EvaluateCart("hero", "mordor", "mordor", cart);

        Assert.AreSame(RecruitGateResult.Allowed, result);
        _service.DidNotReceiveWithAnyArgs().CanAffordRecruit(default, default, default, default);
    }

    [TestMethod]
    public void EvaluateCart_ResourceMode_DelegatesToTheService()
    {
        var cart = new List<RecruitCartEntry> { new("uruk_a", 2) };
        var blocked = new RecruitGateResult(true, 8, "War Spoils");
        _service.CanAffordRecruit("hero", "mordor", "mordor", cart).Returns(blocked);

        var result = new RecruitmentResourceGateHook(_service).EvaluateCart("hero", "mordor", "mordor", cart);

        Assert.AreSame(blocked, result);
    }
}
