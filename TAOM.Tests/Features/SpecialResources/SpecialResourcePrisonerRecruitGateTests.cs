using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.SpecialResources;
using TAOM.Features.SpecialResources.Domain;
using TAOM.Features.SpecialResources.Hooks;

namespace TAOM.Tests.Features.SpecialResources;

/// <summary>
/// #563: a prisoner recruited in the party screen pays its recruit_cost at Done, once per unit
/// (OnUnitRecruited), so Patch26 clamps the RecruitTroop command to what the balance covers after the
/// upgrades queued and the prisoners already recruited in the same screen. Real storage, so the
/// balance read back is the saved one.
/// </summary>
[TestClass]
public class SpecialResourcePrisonerRecruitGateTests
{
    private const string HeroId = "hero_player";
    private const string Kingdom = "empire";
    private const string Culture = "gondor";
    private const string Troll = "cave_troll";             // recruit_cost 50
    private const string Spider = "taom_spider_creature";  // recruit_cost 5
    private const string Captain = "mordor_uruk_captain";  // upgrade_cost 60, no recruit_cost
    private const string Plain = "plain_troop";            // no cost row

    private ISpecialResourceConfigProvider _config = null!;
    private SpecialResourceStorageService _storage = null!;
    private SpecialResourceService _service = null!;

    private static readonly SpecialResource Castar = new(
        id: "castar",
        kingdomIds: new[] { "empire" },
        cultureIds: new[] { "gondor" },
        displayName: "Castar",
        iconSpriteName: "taom_castar_icon",
        cap: 10000f,
        startingAmount: 0f,
        dailyPerTown: 0.5f,
        perBattleVictoryBase: 10f,
        perRaid: 8f,
        perSiegeVictory: 15f,
        perPrisoner: 1f);

    private static IReadOnlyList<RecruitCartEntry> None => new List<RecruitCartEntry>();

    [TestInitialize]
    public void Setup()
    {
        _config = Substitute.For<ISpecialResourceConfigProvider>();
        _config.GetByKingdomId(Kingdom).Returns(Castar);
        _config.GetByCultureId(Culture).Returns(Castar);
        _config.GetTroopCost(Troll).Returns(new TroopResourceCostEntry(Troll, "castar", upgradeCost: 0, dailyUpkeep: 5f, recruitCost: 50));
        _config.GetTroopCost(Spider).Returns(new TroopResourceCostEntry(Spider, "castar", upgradeCost: 0, dailyUpkeep: 0.5f, recruitCost: 5));
        _config.GetTroopCost(Captain).Returns(new TroopResourceCostEntry(Captain, "castar", upgradeCost: 60, dailyUpkeep: 0.2f));
        _config.GetTroopCost(Plain).Returns((TroopResourceCostEntry)null);
        _storage = new SpecialResourceStorageService();
        _service = new SpecialResourceService(_config, _storage, Substitute.For<IModLogger>());
    }

    private void SetBalance(float amount) => _storage.Set(HeroId, "castar", amount);

    private float Balance => _storage.Get(HeroId, "castar");

    private int Clamp(string troopId, int requested, IReadOnlyList<RecruitCartEntry> earlier) =>
        _service.ClampRecruitCount(HeroId, Kingdom, Culture, troopId, requested, earlier);

    [TestMethod]
    public void ClampRecruitCount_BalanceCoversTheWholeRequest_ReturnsTheRequest()
    {
        SetBalance(150f);
        Assert.AreEqual(3, Clamp(Troll, 3, None));
    }

    [TestMethod]
    public void ClampRecruitCount_BalanceCoversPartOfTheRequest_ClampsToWhatItCovers()
    {
        SetBalance(120f);
        Assert.AreEqual(2, Clamp(Troll, 3, None));
    }

    [TestMethod]
    public void ClampRecruitCount_BalanceBelowOneRecruit_ReturnsZero()
    {
        SetBalance(49f);
        Assert.AreEqual(0, Clamp(Troll, 1, None));
    }

    [TestMethod]
    public void ClampRecruitCount_ZeroBalance_ReturnsZero() =>
        Assert.AreEqual(0, Clamp(Troll, 1, None));

    [TestMethod]
    public void ClampRecruitCount_PrisonersAlreadyRecruitedThisScreen_CountAgainstTheBalance()
    {
        SetBalance(120f);
        var earlier = new List<RecruitCartEntry> { new(Troll, 2) }; // 100 owed at Done

        Assert.AreEqual(0, Clamp(Troll, 1, earlier));
    }

    [TestMethod]
    public void ClampRecruitCount_EarlierRecruitsOfOtherTroops_CountOnlyTheirRecruitCost()
    {
        SetBalance(60f);
        var earlier = new List<RecruitCartEntry> { new(Troll, 1), new(Plain, 4), new(Captain, 2) }; // 50 owed

        Assert.AreEqual(2, Clamp(Spider, 3, earlier)); // 10 left, 5 each
    }

    [TestMethod]
    public void ClampRecruitCount_UpgradeQueuedThisScreen_CountsAgainstTheBalance()
    {
        SetBalance(100f);
        _service.BeginPartyScreenSession();
        _service.QueueUpgradeSpend(HeroId, Captain, 1); // 60 pending

        Assert.AreEqual(0, Clamp(Troll, 1, None));
    }

    [TestMethod]
    public void ClampRecruitCount_TroopWithoutARecruitCost_ReturnsTheRequestAtZeroBalance()
    {
        Assert.AreEqual(4, Clamp(Plain, 4, None));
        Assert.AreEqual(4, Clamp(Captain, 4, None));
    }

    [TestMethod]
    public void ClampRecruitCount_NoResourceForTheKingdomOrCulture_ReturnsTheRequest() =>
        Assert.AreEqual(2, _service.ClampRecruitCount(HeroId, "unknown_kingdom", "unknown_culture", Troll, 2, None));

    [TestMethod]
    public void ClampRecruitCount_AnyRequest_NeverWritesTheBalance()
    {
        SetBalance(120f);
        Clamp(Troll, 3, None);
        Assert.AreEqual(120f, Balance);
    }

    [TestMethod]
    public void ClampRecruitCount_NonFiniteBalance_ReturnsZero()
    {
        var storage = Substitute.For<ISpecialResourceStorageService>();
        storage.Get(HeroId, "castar").Returns(float.NaN);
        var service = new SpecialResourceService(_config, storage, Substitute.For<IModLogger>());

        Assert.AreEqual(0, service.ClampRecruitCount(HeroId, Kingdom, Culture, Troll, 1, None));
    }

    [TestMethod]
    public void ChargeRecruitCost_AfterAClampedRequest_ChargesInFullWithoutFlooring()
    {
        SetBalance(120f);

        var clamped = Clamp(Troll, 3, None);
        var charged = _service.ChargeRecruitCost(HeroId, Kingdom, Culture, Troll, clamped);

        Assert.AreEqual(2, clamped);
        Assert.AreEqual(100f, charged);
        Assert.AreEqual(20f, Balance);
    }

    [TestMethod]
    public void ChargeRecruitCost_AfterAnAffordableRequest_ChargesInFull()
    {
        SetBalance(150f);

        var clamped = Clamp(Troll, 3, None);
        var charged = _service.ChargeRecruitCost(HeroId, Kingdom, Culture, Troll, clamped);

        Assert.AreEqual(3, clamped);
        Assert.AreEqual(150f, charged);
        Assert.AreEqual(0f, Balance);
    }

    [TestMethod]
    public void GetRecruitCost_RowsWithAndWithoutARecruitCost_ReturnTheCostOrZero()
    {
        var hook = new PartyUpgradeResourceCheckHook(_service, _config,
            Substitute.For<TAOM.Features.CoopInterop.ICoopSessionProvider>());

        Assert.AreEqual(50, hook.GetRecruitCost(Troll));
        Assert.AreEqual(0, hook.GetRecruitCost(Captain));
        Assert.AreEqual(0, hook.GetRecruitCost(Plain));
    }
}
