using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Arachnophobia;
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
    private IArmouryPlayerAdapter _player;
    private ISpecialResourceSettingsProvider _goldOption;
    private IOutcomeLog _outcomes;
    private IArachnophobiaService _arachnophobia;
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
        _player = Substitute.For<IArmouryPlayerAdapter>();
        _goldOption = Substitute.For<ISpecialResourceSettingsProvider>();
        _outcomes = Substitute.For<IOutcomeLog>();
        _arachnophobia = Substitute.For<IArachnophobiaService>();
        _logger = Substitute.For<IModLogger>();

        _settings.IsEnabled.Returns(true);
        _config.GetConfig().Returns(MakeConfig(
            keySettlements: new[] { "town_EW1" },
            offers: ("gondor", new[] { "gondor_a", "gondor_b" })));

        _resourceService.ResolveResource(Kingdom, Culture).Returns(Caster);
        _resourceConfig.GetTroopCost("gondor_a").Returns(Cost("gondor_a", 30));
        _resourceConfig.GetTroopCost("gondor_b").Returns(Cost("gondor_b", 50));

        _service = new EliteEmissaryService(_config, _settings, _resourceService, _resourceConfig, _party, _player, _goldOption, _outcomes, _arachnophobia, _logger);
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
        Assert.AreEqual(30, list.Offers[0].UnitPrice);
        Assert.AreEqual("gondor_b", list.Offers[1].TroopId);
    }

    // Troops whose rows carry an emissary price (merchant_cost times their resource's multiplier, folded in at load).
    private void PriceTheOffersAtTwentyTimes()
    {
        _resourceConfig.GetTroopCost("gondor_a").Returns(
            new TroopResourceCostEntry("gondor_a", "caster", upgradeCost: 0, dailyUpkeep: 0f, recruitCost: 0, merchantCost: 30, emissaryPrice: 600));
        _resourceConfig.GetTroopCost("gondor_b").Returns(
            new TroopResourceCostEntry("gondor_b", "caster", upgradeCost: 0, dailyUpkeep: 0f, recruitCost: 0, merchantCost: 50, emissaryPrice: 1000));
    }

    [TestMethod]
    public void BuildOfferList_TroopsWithEmissaryPrices_ListsThosePrices()
    {
        PriceTheOffersAtTwentyTimes();
        _resourceService.GetCurrentAmount(Hero, Kingdom, Culture).Returns(600f);

        var list = _service.BuildOfferList(Hero, Kingdom, Culture);

        Assert.AreEqual(600, list.Offers[0].UnitPrice);
        Assert.IsTrue(list.Offers[0].CanAfford);
        Assert.AreEqual(1000, list.Offers[1].UnitPrice);
        Assert.IsFalse(list.Offers[1].CanAfford);
    }

    [TestMethod]
    public void BuildOfferList_OwnerResourceDiffersFromTheTroopsRow_StillListsTheTroopsPrice()
    {
        // A clan whose kingdom resolves to another resource than its culture's troops use: the price follows the
        // troop row, not the resource the owner resolves to.
        PriceTheOffersAtTwentyTimes();
        var otherResource = new SpecialResource(
            id: "war_drums", kingdomIds: new[] { "aserai" }, cultureIds: new string[0],
            displayName: "War Drums", iconSpriteName: "i", cap: 10000f, startingAmount: 0f, dailyPerTown: 0f,
            perBattleVictoryBase: 10f, perRaid: 0f, perSiegeVictory: 0f, perPrisoner: 0f);
        _resourceService.ResolveResource(Kingdom, Culture).Returns(otherResource);
        _resourceService.GetCurrentAmount(Hero, Kingdom, Culture).Returns(100000f);

        var list = _service.BuildOfferList(Hero, Kingdom, Culture);

        Assert.AreEqual(600, list.Offers[0].UnitPrice);
    }

    [TestMethod]
    public void BuildOfferList_GoldOnly_UsesTheRawMerchantCostNotTheEmissaryPrice()
    {
        // The gold price is the raw merchant_cost times the gold rate. The emissary price is for the resource only.
        PriceTheOffersAtTwentyTimes();
        _goldOption.EliteTroopsCostGoldOnly.Returns(true);
        _goldOption.EmissaryGoldPerResource.Returns(200);
        _player.Gold.Returns(1000000);

        var list = _service.BuildOfferList(Hero, Kingdom, Culture);

        Assert.AreEqual(6000, list.Offers[0].UnitPrice); // 30 x 200, not 600 x 200
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
    public void BuildOfferList_HiddenTroop_IsNotOffered()
    {
        _arachnophobia.IsHidden("gondor_a").Returns(true);
        _resourceService.GetCurrentAmount(Hero, Kingdom, Culture).Returns(100f);

        var list = _service.BuildOfferList(Hero, Kingdom, Culture);

        Assert.AreEqual(1, list.Offers.Count);
        Assert.AreEqual("gondor_b", list.Offers[0].TroopId);
    }

    [TestMethod]
    public void Purchase_HiddenTroop_NotOffered()
    {
        _arachnophobia.IsHidden("gondor_a").Returns(true);

        var r = _service.Purchase(Hero, Kingdom, Culture, "gondor_a", 1);

        Assert.AreEqual(EmissaryPurchaseStatus.NotOffered, r.Status);
        _party.DidNotReceive().GrantTroop(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void HasPurchasableOffers_EveryOfferHidden_ReturnsFalse()
    {
        _arachnophobia.IsHidden(Arg.Any<string>()).Returns(true);
        Assert.IsFalse(_service.HasPurchasableOffers(Kingdom, Culture));
    }

    [TestMethod]
    public void BuildOfferList_AffordFlag_FlipsAtBalanceEqualsCost()
    {
        _resourceService.GetCurrentAmount(Hero, Kingdom, Culture).Returns(30f); // exactly one gondor_a
        var list = _service.BuildOfferList(Hero, Kingdom, Culture);

        var a = list.Offers[0]; // cost 30
        var b = list.Offers[1]; // cost 50
        Assert.IsTrue(a.CanAfford);
        Assert.AreEqual(1, a.MaxQuantity);
        Assert.IsFalse(b.CanAfford);
        Assert.AreEqual(0, b.MaxQuantity);
    }

    [TestMethod]
    public void BuildOfferList_MaxAffordable_IsFloorOfBalanceOverCost()
    {
        _resourceService.GetCurrentAmount(Hero, Kingdom, Culture).Returns(95f);
        var list = _service.BuildOfferList(Hero, Kingdom, Culture);
        Assert.AreEqual(3, list.Offers[0].MaxQuantity); // 95 / 30 = 3
        Assert.AreEqual(1, list.Offers[1].MaxQuantity); // 95 / 50 = 1
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

    // ── Gold-only option (#1108) ──

    private const string GoldCheckId = "taom.elite-emissary.gold-purchase";

    private static readonly SpecialResource WarSpoils = new(
        id: "war_spoils",
        kingdomIds: new[] { "empire_s" },
        cultureIds: new[] { "mordor" },
        displayName: "War Spoils",
        iconSpriteName: "SpecialResources\\taom_war_spoils_icon",
        cap: 500f, startingAmount: 0f, dailyPerTown: 0.2f,
        perBattleVictoryBase: 14f, perRaid: 12f, perSiegeVictory: 20f, perPrisoner: 2f);

    private void TurnGoldOn(int rate = 200, int gold = 0, int freeSlots = int.MaxValue)
    {
        _goldOption.EliteTroopsCostGoldOnly.Returns(true);
        _goldOption.EmissaryGoldPerResource.Returns(rate);
        _player.Gold.Returns(gold);
        _player.ChargeGold(Arg.Any<int>()).Returns(true);
        _party.GetMainPartyFreeSlots().Returns(freeSlots);
        SetBuyer(Kingdom, Culture);
    }

    private void SetBuyer(string kingdomId, string cultureId)
    {
        _player.KingdomId.Returns(kingdomId);
        _player.CultureId.Returns(cultureId);
    }

    private void AssertNoGoldTaken() => _player.DidNotReceive().ChargeGold(Arg.Any<int>());

    private void AssertNoResourceTaken() =>
        _resourceService.DidNotReceive().ChargeMerchantPurchase(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());

    [TestMethod]
    public void HasPurchasableOffers_GoldOn_BuyerHoldsTheOwnersResource_ReturnsTrue()
    {
        TurnGoldOn();
        Assert.IsTrue(_service.HasPurchasableOffers(Kingdom, Culture));
    }

    [TestMethod]
    public void HasPurchasableOffers_GoldOn_BuyerFromAnotherFactionOfTheSameGroup_ReturnsTrue()
    {
        TurnGoldOn();
        SetBuyer("arthedain", "arthedain");
        _resourceService.ResolveResource("arthedain", "arthedain").Returns(Caster);

        Assert.IsTrue(_service.HasPurchasableOffers(Kingdom, Culture));
    }

    [TestMethod]
    public void HasPurchasableOffers_GoldOn_BuyerHoldsAnotherResource_ReturnsFalse()
    {
        TurnGoldOn();
        SetBuyer("empire_s", "mordor");
        _resourceService.ResolveResource("empire_s", "mordor").Returns(WarSpoils);

        Assert.IsFalse(_service.HasPurchasableOffers(Kingdom, Culture));
    }

    [TestMethod]
    public void HasPurchasableOffers_GoldOn_BuyerHasNoResource_ReturnsFalse()
    {
        TurnGoldOn();
        SetBuyer("lindon", "lindon");
        _resourceService.ResolveResource("lindon", "lindon").Returns((SpecialResource)null);

        Assert.IsFalse(_service.HasPurchasableOffers(Kingdom, Culture));
    }

    [TestMethod]
    public void HasPurchasableOffers_GoldOff_BuyerHoldsAnotherResource_StillReturnsTrue()
    {
        SetBuyer("empire_s", "mordor");
        _resourceService.ResolveResource("empire_s", "mordor").Returns(WarSpoils);

        Assert.IsTrue(_service.HasPurchasableOffers(Kingdom, Culture));
        _ = _player.DidNotReceive().KingdomId;
    }

    [TestMethod]
    public void BuildOfferList_GoldOn_PricesAtMerchantCostTimesTheRate()
    {
        TurnGoldOn(rate: 137, gold: 100000);

        var list = _service.BuildOfferList(Hero, Kingdom, Culture);

        Assert.AreEqual(30 * 137, list.Offers[0].UnitPrice);
        Assert.AreEqual(50 * 137, list.Offers[1].UnitPrice);
    }

    [TestMethod]
    public void BuildOfferList_GoldOn_ReadsThePurse_NotTheResourceBalance()
    {
        TurnGoldOn(rate: 200, gold: 12000);
        _resourceService.GetCurrentAmount(Hero, Kingdom, Culture).Returns(9999f);

        var list = _service.BuildOfferList(Hero, Kingdom, Culture);

        Assert.AreEqual(12000f, list.PlayerBalance);
        Assert.AreEqual(2, list.Offers[0].MaxQuantity);
        Assert.AreEqual(1, list.Offers[1].MaxQuantity);
    }

    [TestMethod]
    public void BuildOfferList_GoldOn_AffordFlag_FlipsAtGoldEqualsPrice()
    {
        TurnGoldOn(rate: 200, gold: 10000);
        Assert.IsTrue(_service.BuildOfferList(Hero, Kingdom, Culture).Offers[1].CanAfford);

        _player.Gold.Returns(9999);
        Assert.IsFalse(_service.BuildOfferList(Hero, Kingdom, Culture).Offers[1].CanAfford);
    }

    [TestMethod]
    public void BuildOfferList_GoldOn_GivesNoResourceName_SoThePresenterShowsDenars()
    {
        TurnGoldOn(gold: 1000);
        Assert.IsNull(_service.BuildOfferList(Hero, Kingdom, Culture).ResourceDisplayName);
    }

    [TestMethod]
    public void BuildOfferList_GoldOff_StaysInTheResource()
    {
        _resourceService.GetCurrentAmount(Hero, Kingdom, Culture).Returns(60f);

        var list = _service.BuildOfferList(Hero, Kingdom, Culture);

        Assert.AreEqual("Castar", list.ResourceDisplayName);
        Assert.AreEqual(60f, list.PlayerBalance);
        Assert.AreEqual(30, list.Offers[0].UnitPrice);
        _ = _player.DidNotReceive().Gold;
    }

    [TestMethod]
    public void Purchase_GoldOn_GrantsThenChargesGold_AndNoResource()
    {
        TurnGoldOn(rate: 137, gold: 20000);
        _party.GrantTroop("gondor_a", 2).Returns(true);

        var r = _service.Purchase(Hero, Kingdom, Culture, "gondor_a", 2);

        Assert.AreEqual(EmissaryPurchaseStatus.Success, r.Status);
        Assert.AreEqual(8220, r.TotalCost);
        Assert.IsNull(r.ResourceDisplayName);
        Received.InOrder(() =>
        {
            _party.GrantTroop("gondor_a", 2);
            _player.ChargeGold(8220);
        });
        AssertNoResourceTaken();
    }

    [TestMethod]
    public void Purchase_GoldOn_ExactGold_Succeeds()
    {
        TurnGoldOn(rate: 200, gold: 6000);
        _party.GrantTroop("gondor_a", 1).Returns(true);

        Assert.AreEqual(EmissaryPurchaseStatus.Success, _service.Purchase(Hero, Kingdom, Culture, "gondor_a", 1).Status);
    }

    [TestMethod]
    public void Purchase_GoldOn_OneGoldShort_UnaffordableAndNothingMoves()
    {
        TurnGoldOn(rate: 200, gold: 5999);
        _resourceService.CanAffordMerchantPurchase(Hero, Kingdom, Culture, "gondor_a", 1).Returns(true);

        var r = _service.Purchase(Hero, Kingdom, Culture, "gondor_a", 1);

        Assert.AreEqual(EmissaryPurchaseStatus.Unaffordable, r.Status);
        Assert.AreEqual(6000, r.TotalCost);
        Assert.IsNull(r.ResourceDisplayName);
        _party.DidNotReceive().GrantTroop(Arg.Any<string>(), Arg.Any<int>());
        AssertNoGoldTaken();
        AssertNoResourceTaken();
        _outcomes.DidNotReceive().Pass(GoldCheckId, Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void BuildOfferList_GoldOn_MaxQuantity_IsTheFloorOfTheExactGold()
    {
        TurnGoldOn(rate: 50, gold: int.MaxValue);
        _resourceConfig.GetTroopCost("gondor_a").Returns(Cost("gondor_a", 6));

        var offer = _service.BuildOfferList(Hero, Kingdom, Culture).Offers[0];

        Assert.AreEqual(7158278, offer.MaxQuantity);
    }

    [TestMethod]
    public void BuildOfferList_GoldOn_BalanceAboveFloatPrecision_StaysExact()
    {
        TurnGoldOn(rate: 200, gold: 16777217);

        Assert.AreEqual(16777217d, _service.BuildOfferList(Hero, Kingdom, Culture).PlayerBalance);
    }

    [TestMethod]
    public void Purchase_GoldOn_TotalPastIntMax_UnaffordableAndNothingMoves()
    {
        TurnGoldOn(rate: 50, gold: int.MaxValue);
        _resourceConfig.GetTroopCost("gondor_a").Returns(Cost("gondor_a", 6));
        _party.GrantTroop("gondor_a", 7158279).Returns(true);

        var r = _service.Purchase(Hero, Kingdom, Culture, "gondor_a", 7158279);

        Assert.AreEqual(EmissaryPurchaseStatus.Unaffordable, r.Status);
        Assert.AreEqual(int.MaxValue, r.TotalCost);
        _party.DidNotReceive().GrantTroop(Arg.Any<string>(), Arg.Any<int>());
        AssertNoGoldTaken();
    }

    [TestMethod]
    public void Purchase_GoldOn_TheOfferedMaximum_SucceedsAndChargesTheExactTotal()
    {
        TurnGoldOn(rate: 50, gold: int.MaxValue);
        _resourceConfig.GetTroopCost("gondor_a").Returns(Cost("gondor_a", 6));
        var max = _service.BuildOfferList(Hero, Kingdom, Culture).Offers[0].MaxQuantity;
        _party.GrantTroop("gondor_a", max).Returns(true);

        var r = _service.Purchase(Hero, Kingdom, Culture, "gondor_a", max);

        Assert.AreEqual(EmissaryPurchaseStatus.Success, r.Status);
        _player.Received(1).ChargeGold(2147483400);
    }

    [TestMethod]
    public void Purchase_GoldOn_GrantFails_FailedAndTakesNoGold()
    {
        TurnGoldOn(rate: 200, gold: 20000);
        _party.GrantTroop("gondor_a", 1).Returns(false);

        var r = _service.Purchase(Hero, Kingdom, Culture, "gondor_a", 1);

        Assert.AreEqual(EmissaryPurchaseStatus.Failed, r.Status);
        AssertNoGoldTaken();
    }

    [TestMethod]
    public void Purchase_GoldOn_Success_WritesTheCheckLine()
    {
        TurnGoldOn(rate: 200, gold: 20000);
        _party.GrantTroop("gondor_a", 1).Returns(true);

        _service.Purchase(Hero, Kingdom, Culture, "gondor_a", 1);

        _outcomes.Received(1).Pass(GoldCheckId, "", "troop=gondor_a qty=1 gold=6000");
    }

    [TestMethod]
    public void Purchase_GoldOn_ChargeRefusedAfterGrant_WritesTheFailLine()
    {
        TurnGoldOn(rate: 200, gold: 20000);
        _party.GrantTroop("gondor_a", 1).Returns(true);
        _player.ChargeGold(6000).Returns(false);

        var r = _service.Purchase(Hero, Kingdom, Culture, "gondor_a", 1);

        Assert.AreEqual(EmissaryPurchaseStatus.Success, r.Status);
        _outcomes.Received(1).Fail(GoldCheckId, "", "troop=gondor_a qty=1 gold=6000");
        _outcomes.DidNotReceive().Pass(GoldCheckId, Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void Purchase_GoldOff_ChargesTheResource_AndTakesNoGold()
    {
        _resourceService.CanAffordMerchantPurchase(Hero, Kingdom, Culture, "gondor_a", 1).Returns(true);
        _party.GrantTroop("gondor_a", 1).Returns(true);

        var r = _service.Purchase(Hero, Kingdom, Culture, "gondor_a", 1);

        Assert.AreEqual("Castar", r.ResourceDisplayName);
        Assert.AreEqual(30, r.TotalCost);
        _resourceService.Received(1).ChargeMerchantPurchase(Hero, Kingdom, Culture, "gondor_a", 1);
        AssertNoGoldTaken();
        _outcomes.DidNotReceive().Pass(GoldCheckId, Arg.Any<string>(), Arg.Any<string>());
    }

    // ── Gold-only: the party limit ──

    [TestMethod]
    public void BuildOfferList_GoldOn_RoomBelowTheGoldLimit_CapsAtTheRoomAndNamesTheParty()
    {
        TurnGoldOn(rate: 100, gold: 100000, freeSlots: 2);

        var offer = _service.BuildOfferList(Hero, Kingdom, Culture).Offers[0];

        Assert.AreEqual(2, offer.MaxQuantity);
        Assert.IsTrue(offer.CanAfford);
        Assert.IsTrue(offer.LimitedByPartySize);
    }

    [TestMethod]
    public void BuildOfferList_GoldOn_PartyFull_OffersNoneButStaysAffordable()
    {
        TurnGoldOn(rate: 100, gold: 100000, freeSlots: 0);

        var offer = _service.BuildOfferList(Hero, Kingdom, Culture).Offers[0];

        Assert.AreEqual(0, offer.MaxQuantity);
        Assert.IsTrue(offer.CanAfford);
        Assert.IsTrue(offer.LimitedByPartySize);
    }

    [TestMethod]
    public void BuildOfferList_GoldOn_GoldBelowTheRoom_KeepsTheGoldLimit()
    {
        TurnGoldOn(rate: 100, gold: 6000, freeSlots: 50);

        var offer = _service.BuildOfferList(Hero, Kingdom, Culture).Offers[0];

        Assert.AreEqual(2, offer.MaxQuantity);
        Assert.IsFalse(offer.LimitedByPartySize);
    }

    [TestMethod]
    public void BuildOfferList_GoldOn_GoldEqualsTheRoom_IsNotLimitedByTheParty()
    {
        TurnGoldOn(rate: 100, gold: 6000, freeSlots: 2);

        var offer = _service.BuildOfferList(Hero, Kingdom, Culture).Offers[0];

        Assert.AreEqual(2, offer.MaxQuantity);
        Assert.IsFalse(offer.LimitedByPartySize);
    }

    [TestMethod]
    public void BuildOfferList_GoldOn_NoGoldAndNoRoom_ReportsTheGoldNotTheParty()
    {
        TurnGoldOn(rate: 100, gold: 0, freeSlots: 0);

        var offer = _service.BuildOfferList(Hero, Kingdom, Culture).Offers[0];

        Assert.IsFalse(offer.CanAfford);
        Assert.IsFalse(offer.LimitedByPartySize);
    }

    [TestMethod]
    public void BuildOfferList_GoldOff_NeverReadsTheParty()
    {
        _resourceService.GetCurrentAmount(Hero, Kingdom, Culture).Returns(100f);

        var offer = _service.BuildOfferList(Hero, Kingdom, Culture).Offers[0];

        Assert.AreEqual(3, offer.MaxQuantity);
        Assert.IsFalse(offer.LimitedByPartySize);
        _party.DidNotReceive().GetMainPartyFreeSlots();
        _party.DidNotReceive().GetPartySlotsPerTroop(Arg.Any<string>());
    }

    [TestMethod]
    public void Purchase_GoldOn_MoreThanTheRoom_PartyFullAndNothingMoves()
    {
        TurnGoldOn(rate: 100, gold: 100000, freeSlots: 2);
        _party.GrantTroop("gondor_a", 3).Returns(true);

        var r = _service.Purchase(Hero, Kingdom, Culture, "gondor_a", 3);

        Assert.AreEqual(EmissaryPurchaseStatus.PartyFull, r.Status);
        _party.DidNotReceive().GrantTroop(Arg.Any<string>(), Arg.Any<int>());
        AssertNoGoldTaken();
        AssertNoResourceTaken();
    }

    [TestMethod]
    public void Purchase_GoldOn_ExactlyTheRoom_Succeeds()
    {
        TurnGoldOn(rate: 100, gold: 100000, freeSlots: 2);
        _party.GrantTroop("gondor_a", 2).Returns(true);

        var r = _service.Purchase(Hero, Kingdom, Culture, "gondor_a", 2);

        Assert.AreEqual(EmissaryPurchaseStatus.Success, r.Status);
        _player.Received(1).ChargeGold(6000);
    }

    [TestMethod]
    public void Purchase_GoldOn_NoGoldAndNoRoom_ReportsTheGold()
    {
        TurnGoldOn(rate: 100, gold: 0, freeSlots: 0);

        var r = _service.Purchase(Hero, Kingdom, Culture, "gondor_a", 1);

        Assert.AreEqual(EmissaryPurchaseStatus.Unaffordable, r.Status);
    }

    [TestMethod]
    public void Purchase_GoldOff_MoreThanTheRoom_StillSucceeds()
    {
        _party.GetMainPartyFreeSlots().Returns(0);
        _resourceService.CanAffordMerchantPurchase(Hero, Kingdom, Culture, "gondor_a", 3).Returns(true);
        _party.GrantTroop("gondor_a", 3).Returns(true);

        var r = _service.Purchase(Hero, Kingdom, Culture, "gondor_a", 3);

        Assert.AreEqual(EmissaryPurchaseStatus.Success, r.Status);
        _party.DidNotReceive().GetMainPartyFreeSlots();
        _party.DidNotReceive().GetPartySlotsPerTroop(Arg.Any<string>());
    }

    // ── Gold-only: Troop Weight. The free slots are weighted, so one troop fills its weight. ──

    [TestMethod]
    public void BuildOfferList_GoldOn_HeavyTroop_RoomIsTheFreeSlotsOverItsWeight()
    {
        TurnGoldOn(rate: 100, gold: 100000, freeSlots: 10);
        _party.GetPartySlotsPerTroop("gondor_a").Returns(3f);

        var offer = _service.BuildOfferList(Hero, Kingdom, Culture).Offers[0];

        Assert.AreEqual(3, offer.MaxQuantity);
        Assert.IsTrue(offer.LimitedByPartySize);
    }

    [TestMethod]
    public void BuildOfferList_GoldOn_TroopHeavierThanTheRoom_OffersNoneButStaysAffordable()
    {
        TurnGoldOn(rate: 100, gold: 100000, freeSlots: 10);
        _party.GetPartySlotsPerTroop("gondor_a").Returns(20f);

        var offer = _service.BuildOfferList(Hero, Kingdom, Culture).Offers[0];

        Assert.AreEqual(0, offer.MaxQuantity);
        Assert.IsTrue(offer.CanAfford);
        Assert.IsTrue(offer.LimitedByPartySize);
    }

    [TestMethod]
    public void BuildOfferList_GoldOn_InfiniteWeight_OffersNone()
    {
        TurnGoldOn(rate: 100, gold: 100000, freeSlots: 10);
        _party.GetPartySlotsPerTroop("gondor_a").Returns(float.PositiveInfinity);

        Assert.AreEqual(0, _service.BuildOfferList(Hero, Kingdom, Culture).Offers[0].MaxQuantity);
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(0f)]
    [DataRow(-2f)]
    [DataRow(0.5f)]
    [DataRow(1f)]
    public void BuildOfferList_GoldOn_SlotsPerTroopNotAboveOne_FillsOneSlotEach(float slots)
    {
        TurnGoldOn(rate: 100, gold: 100000, freeSlots: 4);
        _party.GetPartySlotsPerTroop("gondor_a").Returns(slots);

        Assert.AreEqual(4, _service.BuildOfferList(Hero, Kingdom, Culture).Offers[0].MaxQuantity);
    }

    [TestMethod]
    public void Purchase_GoldOn_HeavyTroop_MoreThanTheWeightedRoom_PartyFullAndNothingMoves()
    {
        TurnGoldOn(rate: 100, gold: 100000, freeSlots: 10);
        _party.GetPartySlotsPerTroop("gondor_a").Returns(3f);
        _party.GrantTroop("gondor_a", 4).Returns(true);

        var r = _service.Purchase(Hero, Kingdom, Culture, "gondor_a", 4);

        Assert.AreEqual(EmissaryPurchaseStatus.PartyFull, r.Status);
        _party.DidNotReceive().GrantTroop(Arg.Any<string>(), Arg.Any<int>());
        AssertNoGoldTaken();
    }

    [TestMethod]
    public void Purchase_GoldOn_HeavyTroop_ExactlyTheWeightedRoom_Succeeds()
    {
        TurnGoldOn(rate: 100, gold: 100000, freeSlots: 10);
        _party.GetPartySlotsPerTroop("gondor_a").Returns(3f);
        _party.GrantTroop("gondor_a", 3).Returns(true);

        var r = _service.Purchase(Hero, Kingdom, Culture, "gondor_a", 3);

        Assert.AreEqual(EmissaryPurchaseStatus.Success, r.Status);
        _player.Received(1).ChargeGold(9000);
    }
}
