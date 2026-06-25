using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.EliteEmissary;
using TAOM.Features.EliteEmissary.Domain;
using TAOM.Features.SpecialResources;
using TAOM.Features.SpecialResources.Domain;

namespace TAOM.Tests.Features.EliteEmissary;

[TestClass]
public class EliteEmissaryServiceTests
{
    private IEliteEmissaryConfigProvider _config;
    private IEliteEmissarySettingsProvider _settings;
    private ISpecialResourceService _resourceService;
    private ISpecialResourceConfigProvider _resourceConfig;
    private IPlayerPartyAdapter _party;
    private IModLogger _logger;
    private EliteEmissaryService _service;

    private static readonly SpecialResource Caster = new(
        id: "caster",
        kingdomIds: new[] { "empire_w" },
        cultureIds: new[] { "gondor" },
        displayName: "Castar",
        iconSpriteName: "SpecialResources\\taom_caster_icon",
        cap: 500f, startingAmount: 25f, dailyPerTown: 0.6f,
        perBattleVictoryBase: 8f, perRaid: 4f, perSiegeVictory: 18f, perPrisoner: 1f);

    private const string Hero = "hero1";
    private const string Kingdom = "empire_w";
    private const string Culture = "gondor";

    [TestInitialize]
    public void Setup()
    {
        _config = Substitute.For<IEliteEmissaryConfigProvider>();
        _settings = Substitute.For<IEliteEmissarySettingsProvider>();
        _resourceService = Substitute.For<ISpecialResourceService>();
        _resourceConfig = Substitute.For<ISpecialResourceConfigProvider>();
        _party = Substitute.For<IPlayerPartyAdapter>();
        _logger = Substitute.For<IModLogger>();

        _settings.IsEnabled.Returns(true);
        _config.GetConfig().Returns(MakeConfig(
            keySettlements: new[] { "town_EW1" },
            offers: ("gondor", new[] { "gondor_a", "gondor_b" })));

        _resourceService.ResolveResource(Kingdom, Culture).Returns(Caster);
        _resourceConfig.GetTroopCost("gondor_a").Returns(Cost("gondor_a", 30));
        _resourceConfig.GetTroopCost("gondor_b").Returns(Cost("gondor_b", 50));

        _service = new EliteEmissaryService(_config, _settings, _resourceService, _resourceConfig, _party, _logger);
    }

    private static TroopResourceCostEntry Cost(string id, int merchant) =>
        new(id, "caster", upgradeCost: 0, dailyUpkeep: 0f, recruitCost: 0, merchantCost: merchant);

    private static EliteEmissaryConfig MakeConfig(string[] keySettlements, params (string culture, string[] troops)[] offers)
    {
        var dict = new Dictionary<string, IReadOnlyList<string>>();
        foreach (var (culture, troops) in offers)
            dict[culture] = new List<string>(troops);
        return new EliteEmissaryConfig(true, new HashSet<string>(keySettlements), dict);
    }

    // ── IsEnabled / IsKeySettlement ──

    [TestMethod]
    public void IsEnabled_ReflectsSettings()
    {
        _settings.IsEnabled.Returns(false);
        Assert.IsFalse(_service.IsEnabled);
    }

    [TestMethod]
    public void IsKeySettlement_ConfiguredId_ReturnsTrue() =>
        Assert.IsTrue(_service.IsKeySettlement("town_EW1"));

    [TestMethod]
    public void IsKeySettlement_UnknownId_ReturnsFalse() =>
        Assert.IsFalse(_service.IsKeySettlement("town_ZZ9"));

    [TestMethod]
    public void IsKeySettlement_Null_ReturnsFalse() =>
        Assert.IsFalse(_service.IsKeySettlement(null));

    // ── HasPurchasableOffers ──

    [TestMethod]
    public void HasPurchasableOffers_CultureWithPricedTroops_ReturnsTrue() =>
        Assert.IsTrue(_service.HasPurchasableOffers(Kingdom, Culture));

    [TestMethod]
    public void HasPurchasableOffers_NoResolvedResource_ReturnsFalse()
    {
        _resourceService.ResolveResource("rebel_x", "umbar").Returns((SpecialResource)null);
        Assert.IsFalse(_service.HasPurchasableOffers("rebel_x", "umbar"));
    }

    [TestMethod]
    public void HasPurchasableOffers_CultureNotInConfig_ReturnsFalse()
    {
        _resourceService.ResolveResource(Kingdom, "mordor").Returns(Caster);
        Assert.IsFalse(_service.HasPurchasableOffers(Kingdom, "mordor"));
    }

    [TestMethod]
    public void HasPurchasableOffers_AllOffersLackMerchantCost_ReturnsFalse()
    {
        _resourceConfig.GetTroopCost("gondor_a").Returns(Cost("gondor_a", 0));
        _resourceConfig.GetTroopCost("gondor_b").Returns((TroopResourceCostEntry)null);
        Assert.IsFalse(_service.HasPurchasableOffers(Kingdom, Culture));
    }

    // ── BuildOfferList ──

    [TestMethod]
    public void BuildOfferList_NoResource_ReturnsNoResourceMarker()
    {
        _resourceService.ResolveResource("rebel_x", "umbar").Returns((SpecialResource)null);
        var list = _service.BuildOfferList(Hero, "rebel_x", "umbar");
        Assert.IsTrue(list.NoResource);
        Assert.IsFalse(list.HasOffers);
    }

    [TestMethod]
    public void BuildOfferList_ReturnsPricedTroops_InConfigOrder()
    {
        _resourceService.GetCurrentAmount(Hero, Kingdom, Culture).Returns(100f);
        var list = _service.BuildOfferList(Hero, Kingdom, Culture);

        Assert.IsFalse(list.NoResource);
        Assert.AreEqual("caster", list.ResourceId);
        Assert.AreEqual("Castar", list.ResourceDisplayName);
        Assert.AreEqual(2, list.Offers.Count);
        Assert.AreEqual("gondor_a", list.Offers[0].TroopId);
        Assert.AreEqual(30, list.Offers[0].MerchantCost);
        Assert.AreEqual("gondor_b", list.Offers[1].TroopId);
    }

    [TestMethod]
    public void BuildOfferList_SkipsTroopsWithoutMerchantCost()
    {
        _resourceConfig.GetTroopCost("gondor_a").Returns((TroopResourceCostEntry)null);
        _resourceService.GetCurrentAmount(Hero, Kingdom, Culture).Returns(100f);

        var list = _service.BuildOfferList(Hero, Kingdom, Culture);

        Assert.AreEqual(1, list.Offers.Count);
        Assert.AreEqual("gondor_b", list.Offers[0].TroopId);
    }

    [TestMethod]
    public void BuildOfferList_AffordFlag_FlipsAtBalanceEqualsCost()
    {
        _resourceService.GetCurrentAmount(Hero, Kingdom, Culture).Returns(30f); // exactly one gondor_a
        var list = _service.BuildOfferList(Hero, Kingdom, Culture);

        var a = list.Offers[0]; // cost 30
        var b = list.Offers[1]; // cost 50
        Assert.IsTrue(a.CanAfford);
        Assert.AreEqual(1, a.MaxAffordableQuantity);
        Assert.IsFalse(b.CanAfford);
        Assert.AreEqual(0, b.MaxAffordableQuantity);
    }

    [TestMethod]
    public void BuildOfferList_MaxAffordable_IsFloorOfBalanceOverCost()
    {
        _resourceService.GetCurrentAmount(Hero, Kingdom, Culture).Returns(95f);
        var list = _service.BuildOfferList(Hero, Kingdom, Culture);
        Assert.AreEqual(3, list.Offers[0].MaxAffordableQuantity); // 95 / 30 = 3
        Assert.AreEqual(1, list.Offers[1].MaxAffordableQuantity); // 95 / 50 = 1
    }

    [TestMethod]
    public void BuildOfferList_CultureNotInConfig_EmptyOffers()
    {
        _resourceService.ResolveResource(Kingdom, "mordor").Returns(Caster);
        var list = _service.BuildOfferList(Hero, Kingdom, "mordor");
        Assert.IsFalse(list.NoResource);
        Assert.IsFalse(list.HasOffers);
    }

    // ── Purchase ──

    [TestMethod]
    public void Purchase_NullTroop_Invalid()
    {
        var r = _service.Purchase(Hero, Kingdom, Culture, null, 1);
        Assert.AreEqual(EmissaryPurchaseStatus.Invalid, r.Status);
        _party.DidNotReceive().GrantTroop(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void Purchase_ZeroQuantity_Invalid()
    {
        var r = _service.Purchase(Hero, Kingdom, Culture, "gondor_a", 0);
        Assert.AreEqual(EmissaryPurchaseStatus.Invalid, r.Status);
    }

    [TestMethod]
    public void Purchase_NoResource_NoResourceStatus()
    {
        _resourceService.ResolveResource("rebel_x", "umbar").Returns((SpecialResource)null);
        var r = _service.Purchase(Hero, "rebel_x", "umbar", "gondor_a", 1);
        Assert.AreEqual(EmissaryPurchaseStatus.NoResource, r.Status);
        _party.DidNotReceive().GrantTroop(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void Purchase_TroopWithoutMerchantCost_NotOffered()
    {
        _resourceConfig.GetTroopCost("gondor_a").Returns(Cost("gondor_a", 0));
        var r = _service.Purchase(Hero, Kingdom, Culture, "gondor_a", 1);
        Assert.AreEqual(EmissaryPurchaseStatus.NotOffered, r.Status);
    }

    [TestMethod]
    public void Purchase_TroopNotInOwnerCultureList_NotOffered()
    {
        // Has a merchant cost, but isn't an offer for gondor — don't trust the inquiry round-trip.
        _resourceConfig.GetTroopCost("mordor_uruk_captain").Returns(Cost("mordor_uruk_captain", 40));
        var r = _service.Purchase(Hero, Kingdom, Culture, "mordor_uruk_captain", 1);
        Assert.AreEqual(EmissaryPurchaseStatus.NotOffered, r.Status);
        _party.DidNotReceive().GrantTroop(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void Purchase_Unaffordable_DoesNotGrantOrCharge()
    {
        _resourceService.CanAffordMerchantPurchase(Hero, Kingdom, Culture, "gondor_a", 3).Returns(false);
        var r = _service.Purchase(Hero, Kingdom, Culture, "gondor_a", 3);

        Assert.AreEqual(EmissaryPurchaseStatus.Unaffordable, r.Status);
        Assert.AreEqual(90, r.TotalCost); // 30 × 3
        Assert.AreEqual("Castar", r.ResourceDisplayName);
        _party.DidNotReceive().GrantTroop(Arg.Any<string>(), Arg.Any<int>());
        _resourceService.DidNotReceive().ChargeMerchantPurchase(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void Purchase_Success_GrantsThenCharges()
    {
        _resourceService.CanAffordMerchantPurchase(Hero, Kingdom, Culture, "gondor_a", 2).Returns(true);
        _party.GrantTroop("gondor_a", 2).Returns(true);

        var r = _service.Purchase(Hero, Kingdom, Culture, "gondor_a", 2);

        Assert.AreEqual(EmissaryPurchaseStatus.Success, r.Status);
        Assert.AreEqual(60, r.TotalCost);
        Assert.AreEqual(2, r.Quantity);
        Assert.AreEqual("gondor_a", r.TroopId);
        _party.Received(1).GrantTroop("gondor_a", 2);
        _resourceService.Received(1).ChargeMerchantPurchase(Hero, Kingdom, Culture, "gondor_a", 2);
    }

    [TestMethod]
    public void Purchase_GrantFails_FailedAndDoesNotCharge()
    {
        _resourceService.CanAffordMerchantPurchase(Hero, Kingdom, Culture, "gondor_a", 1).Returns(true);
        _party.GrantTroop("gondor_a", 1).Returns(false);

        var r = _service.Purchase(Hero, Kingdom, Culture, "gondor_a", 1);

        Assert.AreEqual(EmissaryPurchaseStatus.Failed, r.Status);
        _resourceService.DidNotReceive().ChargeMerchantPurchase(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
    }
}
