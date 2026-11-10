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

    // ── Gold mode (#1109): "Use Special Resources" off, elites sold for merchant_cost x rate in gold ──

    private static readonly SpecialResource OtherResource = new(
        id: "gems",
        kingdomIds: new[] { "dwarves" },
        cultureIds: new[] { "erebor" },
        displayName: "Gems",
        iconSpriteName: "SpecialResources\taom_gems_icon",
        cap: 600f, startingAmount: 10f, dailyPerTown: 0.4f,
        perBattleVictoryBase: 6f, perRaid: 3f, perSiegeVictory: 12f, perPrisoner: 1f);

    private void GoldMode(int gold = 6500, int rate = 100)
    {
        _resourceService.IsGoldMode.Returns(true);
        _resourceService.GoldPerResourcePoint.Returns(rate);
        _party.GetMainHeroKingdomId().Returns(Kingdom);
        _party.GetMainHeroCultureId().Returns(Culture);
        _party.GetMainHeroGold().Returns(gold);
    }

    [TestMethod]
    public void HasPurchasableOffers_GoldMode_BuyerResourceMatches_True()
    {
        GoldMode();

        Assert.IsTrue(_service.HasPurchasableOffers(Kingdom, Culture));
    }

    [TestMethod]
    public void HasPurchasableOffers_GoldMode_BuyerResourceDiffers_False()
    {
        GoldMode();
        _party.GetMainHeroKingdomId().Returns("dwarves");
        _party.GetMainHeroCultureId().Returns("erebor");
        _resourceService.ResolveResource("dwarves", "erebor").Returns(OtherResource);

        Assert.IsFalse(_service.HasPurchasableOffers(Kingdom, Culture));
    }

    [TestMethod]
    public void HasPurchasableOffers_GoldMode_BuyerResourceNull_False()
    {
        GoldMode();
        _party.GetMainHeroKingdomId().Returns("nowhere");
        _party.GetMainHeroCultureId().Returns("nobody");

        Assert.IsFalse(_service.HasPurchasableOffers(Kingdom, Culture));
    }

    [TestMethod]
    public void HasPurchasableOffers_ResourceMode_IgnoresTheBuyer()
    {
        _party.GetMainHeroKingdomId().Returns("dwarves");
        _party.GetMainHeroCultureId().Returns("erebor");

        Assert.IsTrue(_service.HasPurchasableOffers(Kingdom, Culture));
    }

    [TestMethod]
    public void BuildOfferList_GoldMode_PricesMerchantCostTimesRate_AndBalanceIsGold()
    {
        GoldMode(gold: 6500);

        var list = _service.BuildOfferList(Hero, Kingdom, Culture);

        Assert.IsFalse(list.NoResource);
        Assert.IsTrue(list.PaidInGold);
        Assert.AreEqual(6500f, list.PlayerBalance);
        Assert.AreEqual(3000, list.Offers[0].MerchantCost);
        Assert.AreEqual(2, list.Offers[0].MaxAffordableQuantity);
        Assert.AreEqual(5000, list.Offers[1].MerchantCost);
        Assert.AreEqual(1, list.Offers[1].MaxAffordableQuantity);
        _resourceService.DidNotReceive().GetCurrentAmount(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void BuildOfferList_GoldMode_AffordFlipsAtGoldEqualsPrice()
    {
        GoldMode(gold: 3000);
        Assert.IsTrue(_service.BuildOfferList(Hero, Kingdom, Culture).Offers[0].CanAfford);

        GoldMode(gold: 2999);
        Assert.IsFalse(_service.BuildOfferList(Hero, Kingdom, Culture).Offers[0].CanAfford);
    }

    [TestMethod]
    public void BuildOfferList_GoldMode_BuyerResourceDiffers_NoResourceAvailable()
    {
        GoldMode();
        _party.GetMainHeroKingdomId().Returns("dwarves");
        _party.GetMainHeroCultureId().Returns("erebor");
        _resourceService.ResolveResource("dwarves", "erebor").Returns(OtherResource);

        Assert.IsTrue(_service.BuildOfferList(Hero, Kingdom, Culture).NoResource);
    }

    [TestMethod]
    public void Purchase_GoldMode_Success_GrantsThenDebitsGold_NoResourceCharge()
    {
        GoldMode(gold: 6500);
        _party.GrantTroop("gondor_a", 2).Returns(true);
        _party.TryDebitMainHeroGold(6000).Returns(true);

        var r = _service.Purchase(Hero, Kingdom, Culture, "gondor_a", 2);

        Assert.AreEqual(EmissaryPurchaseStatus.Success, r.Status);
        Assert.AreEqual(6000, r.TotalCost);
        Assert.IsTrue(r.PaidInGold);
        Received.InOrder(() =>
        {
            _party.GrantTroop("gondor_a", 2);
            _party.TryDebitMainHeroGold(6000);
        });
        _resourceService.DidNotReceive().ChargeMerchantPurchase(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
        _resourceService.DidNotReceive().CanAffordMerchantPurchase(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void Purchase_GoldMode_Unaffordable_NoGrantNoDebit()
    {
        GoldMode(gold: 5999);

        var r = _service.Purchase(Hero, Kingdom, Culture, "gondor_a", 2);

        Assert.AreEqual(EmissaryPurchaseStatus.Unaffordable, r.Status);
        Assert.AreEqual(6000, r.TotalCost);
        Assert.IsTrue(r.PaidInGold);
        _party.DidNotReceive().GrantTroop(Arg.Any<string>(), Arg.Any<int>());
        _party.DidNotReceive().TryDebitMainHeroGold(Arg.Any<int>());
    }

    // A price can only reach SpecialResourceGoldPrice.MaxGold exactly; past it the total would
    // saturate and undercharge, so no offer or purchase may go past it. gondor_a costs 30 points:
    // 30,000 gold at rate 1000, so 333 units (9,990,000) is the most one purchase may hold.
    [TestMethod]
    public void BuildOfferList_GoldMode_MaxQuantityStopsBelowMaxGold()
    {
        GoldMode(gold: 20_000_000, rate: 1000);

        Assert.AreEqual(333, _service.BuildOfferList(Hero, Kingdom, Culture).Offers[0].MaxAffordableQuantity);
    }

    [TestMethod]
    public void Purchase_GoldMode_TotalPastMaxGold_RejectedNoGrantNoDebit()
    {
        GoldMode(gold: 20_000_000, rate: 1000);

        var r = _service.Purchase(Hero, Kingdom, Culture, "gondor_a", 334);

        Assert.AreEqual(EmissaryPurchaseStatus.Unaffordable, r.Status);
        _party.DidNotReceive().GrantTroop(Arg.Any<string>(), Arg.Any<int>());
        _party.DidNotReceive().TryDebitMainHeroGold(Arg.Any<int>());
    }

    [TestMethod]
    public void Purchase_GoldMode_TotalAtTheCap_ChargesTheExactTotal()
    {
        GoldMode(gold: 20_000_000, rate: 1000);
        _party.GrantTroop("gondor_a", 333).Returns(true);
        _party.TryDebitMainHeroGold(9_990_000).Returns(true);

        var r = _service.Purchase(Hero, Kingdom, Culture, "gondor_a", 333);

        Assert.AreEqual(EmissaryPurchaseStatus.Success, r.Status);
        _party.Received(1).TryDebitMainHeroGold(9_990_000);
    }

    [TestMethod]
    public void Purchase_GoldMode_GrantFails_NoDebit()
    {
        GoldMode(gold: 6500);
        _party.GrantTroop("gondor_a", 2).Returns(false);

        var r = _service.Purchase(Hero, Kingdom, Culture, "gondor_a", 2);

        Assert.AreEqual(EmissaryPurchaseStatus.Failed, r.Status);
        _party.DidNotReceive().TryDebitMainHeroGold(Arg.Any<int>());
    }

    [TestMethod]
    public void Purchase_GoldMode_BuyerResourceDiffers_NotOffered_NoGrantNoDebit()
    {
        GoldMode(gold: 99999);
        _party.GetMainHeroKingdomId().Returns("dwarves");
        _party.GetMainHeroCultureId().Returns("erebor");
        _resourceService.ResolveResource("dwarves", "erebor").Returns(OtherResource);

        var r = _service.Purchase(Hero, Kingdom, Culture, "gondor_a", 1);

        Assert.AreEqual(EmissaryPurchaseStatus.NotOffered, r.Status);
        _party.DidNotReceive().GrantTroop(Arg.Any<string>(), Arg.Any<int>());
        _party.DidNotReceive().TryDebitMainHeroGold(Arg.Any<int>());
    }

    [TestMethod]
    public void Purchase_GoldMode_DebitFailsAfterGrant_StillSuccessAndLogsAnError()
    {
        GoldMode(gold: 6500);
        _party.GrantTroop("gondor_a", 1).Returns(true);
        _party.TryDebitMainHeroGold(3000).Returns(false);

        var r = _service.Purchase(Hero, Kingdom, Culture, "gondor_a", 1);

        Assert.AreEqual(EmissaryPurchaseStatus.Success, r.Status);
        _logger.Received().LogError(Arg.Is<string>(m => m.Contains("debit")));
    }
}
