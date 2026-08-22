using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.SupplyLines;
using TAOM.Features.SupplyLines.Domain;

namespace TAOM.Tests.Features.SupplyLines;

/// <summary>
/// Full-branch coverage for the pure pricing maths: every quote component in isolation, the
/// escort variants, the tier/war scaling, the minimum-hours clamp, and one test per
/// positive-requirement gate (NaN, Infinity, negative) so a corrupt setting or engine value can
/// never reach a quote. The source module carried none of these guards.
/// </summary>
[TestClass]
public class SupplyPricingServiceTests
{
    private ISupplyLinesSettingsProvider _settings;
    private SupplyPricingService _sut;

    [TestInitialize]
    public void Setup()
    {
        // Shipped MCM defaults; individual tests override single knobs.
        _settings = Substitute.For<ISupplyLinesSettingsProvider>();
        _settings.GoodsMarkupFactor.Returns(1.05f);
        _settings.TransportFeePerDistance.Returns(2f);
        _settings.MercenaryWagePerDistance.Returns(10f);
        _settings.CaravanHoursPerDistance.Returns(2f);
        _sut = new SupplyPricingService(_settings);
    }

    // ---- Quote: components in isolation ----

    [TestMethod]
    public void Quote_GoodsOnly_AppliesMarkupFactor()
    {
        var quote = _sut.Quote(1000f, 0, 0f, SupplyEscortOption.None);

        Assert.AreEqual(1050, quote.Goods);
        Assert.AreEqual(0, quote.Transport);
        Assert.AreEqual(0, quote.Guard);
        Assert.AreEqual(0, quote.Troops);
    }

    [TestMethod]
    public void Quote_FractionalGoodsMarkup_RoundsToNearestDenar()
    {
        // 999 * 1.05 = 1048.95 -> 1049
        var quote = _sut.Quote(999f, 0, 0f, SupplyEscortOption.None);

        Assert.AreEqual(1049, quote.Goods);
    }

    [TestMethod]
    public void Quote_DistanceOnly_ChargesTransportFeePerDistance()
    {
        var quote = _sut.Quote(0f, 0, 10f, SupplyEscortOption.None);

        Assert.AreEqual(20, quote.Transport);
        Assert.AreEqual(0, quote.Goods);
    }

    [TestMethod]
    public void Quote_TroopCost_PassesThroughUnchanged()
    {
        // Tier/war scaling already happened in TroopPrice; Quote must not scale twice.
        var quote = _sut.Quote(0f, 730, 0f, SupplyEscortOption.None);

        Assert.AreEqual(730, quote.Troops);
    }

    [TestMethod]
    public void Quote_AllComponents_TotalSumsEverything()
    {
        var quote = _sut.Quote(1000f, 500, 10f, SupplyEscortOption.Mercenaries);

        Assert.AreEqual(1050, quote.Goods);
        Assert.AreEqual(500, quote.Troops);
        Assert.AreEqual(20, quote.Transport);
        Assert.AreEqual(100, quote.Guard);
        Assert.AreEqual(1670, quote.Total);
    }

    // ---- Quote: escort variants ----

    [TestMethod]
    public void Quote_MercenaryEscort_ChargesWagePerDistance()
    {
        var quote = _sut.Quote(0f, 0, 10f, SupplyEscortOption.Mercenaries);

        Assert.AreEqual(100, quote.Guard);
    }

    [TestMethod]
    public void Quote_CompanionEscort_GuardIsFree()
    {
        // The companion is one of the player's own heroes; only mercenaries bill a wage.
        var quote = _sut.Quote(0f, 0, 10f, SupplyEscortOption.Companion);

        Assert.AreEqual(0, quote.Guard);
    }

    [TestMethod]
    public void Quote_NoEscort_GuardIsFree()
    {
        var quote = _sut.Quote(0f, 0, 10f, SupplyEscortOption.None);

        Assert.AreEqual(0, quote.Guard);
    }

    // ---- Quote: hostile inputs (positive-requirement gates) ----

    [TestMethod]
    public void Quote_NaNGoodsValue_GoodsComponentZero()
    {
        var quote = _sut.Quote(float.NaN, 0, 10f, SupplyEscortOption.Mercenaries);

        Assert.AreEqual(0, quote.Goods);
        // The other components must survive one poisoned input.
        Assert.AreEqual(20, quote.Transport);
        Assert.AreEqual(100, quote.Guard);
    }

    [TestMethod]
    public void Quote_NegativeGoodsValue_GoodsComponentZero()
    {
        var quote = _sut.Quote(-500f, 0, 0f, SupplyEscortOption.None);

        Assert.AreEqual(0, quote.Goods);
    }

    [TestMethod]
    public void Quote_NaNDistance_DistanceComponentsZero()
    {
        var quote = _sut.Quote(1000f, 0, float.NaN, SupplyEscortOption.Mercenaries);

        Assert.AreEqual(0, quote.Transport);
        Assert.AreEqual(0, quote.Guard);
        Assert.AreEqual(1050, quote.Goods);
    }

    [TestMethod]
    public void Quote_InfiniteDistance_DistanceComponentsZero()
    {
        var quote = _sut.Quote(0f, 0, float.PositiveInfinity, SupplyEscortOption.Mercenaries);

        Assert.AreEqual(0, quote.Transport);
        Assert.AreEqual(0, quote.Guard);
    }

    [TestMethod]
    public void Quote_NegativeDistance_DistanceComponentsZero()
    {
        var quote = _sut.Quote(0f, 0, -10f, SupplyEscortOption.Mercenaries);

        Assert.AreEqual(0, quote.Transport);
        Assert.AreEqual(0, quote.Guard);
    }

    [TestMethod]
    public void Quote_ZeroDistance_DistanceComponentsZero()
    {
        var quote = _sut.Quote(0f, 0, 0f, SupplyEscortOption.Mercenaries);

        Assert.AreEqual(0, quote.Transport);
        Assert.AreEqual(0, quote.Guard);
    }

    [TestMethod]
    public void Quote_NegativeTroopCost_TroopsComponentZero()
    {
        var quote = _sut.Quote(0f, -100, 0f, SupplyEscortOption.None);

        Assert.AreEqual(0, quote.Troops);
    }

    [TestMethod]
    public void Quote_NaNMarkupSetting_FallsBackToMarketValue()
    {
        // A corrupt multiplier must not make goods free; the fallback charges market value.
        _settings.GoodsMarkupFactor.Returns(float.NaN);

        var quote = _sut.Quote(1000f, 0, 0f, SupplyEscortOption.None);

        Assert.AreEqual(1000, quote.Goods);
    }

    [TestMethod]
    public void Quote_ZeroMarkupSetting_FallsBackToMarketValue()
    {
        _settings.GoodsMarkupFactor.Returns(0f);

        var quote = _sut.Quote(1000f, 0, 0f, SupplyEscortOption.None);

        Assert.AreEqual(1000, quote.Goods);
    }

    [TestMethod]
    public void Quote_NegativeMarkupSetting_FallsBackToMarketValue()
    {
        _settings.GoodsMarkupFactor.Returns(-1f);

        var quote = _sut.Quote(1000f, 0, 0f, SupplyEscortOption.None);

        Assert.AreEqual(1000, quote.Goods);
    }

    [TestMethod]
    public void Quote_NaNTransportFeeSetting_TransportZero()
    {
        _settings.TransportFeePerDistance.Returns(float.NaN);

        var quote = _sut.Quote(0f, 0, 10f, SupplyEscortOption.None);

        Assert.AreEqual(0, quote.Transport);
    }

    [TestMethod]
    public void Quote_InfiniteMercenaryWageSetting_GuardZero()
    {
        _settings.MercenaryWagePerDistance.Returns(float.PositiveInfinity);

        var quote = _sut.Quote(0f, 0, 10f, SupplyEscortOption.Mercenaries);

        Assert.AreEqual(0, quote.Guard);
    }

    // ---- TroopPrice ----

    [TestMethod]
    public void TroopPrice_Tier1Peace_NoScaling()
    {
        Assert.AreEqual(100, _sut.TroopPrice(100, 1, atWar: false));
    }

    [TestMethod]
    public void TroopPrice_Tier3Peace_AddsPremiumPerTierAboveOne()
    {
        // 100 * (1 + 0.15 * 2) = 130
        Assert.AreEqual(130, _sut.TroopPrice(100, 3, atWar: false));
    }

    [TestMethod]
    public void TroopPrice_Tier1AtWar_AppliesWartimeSurcharge()
    {
        Assert.AreEqual(150, _sut.TroopPrice(100, 1, atWar: true));
    }

    [TestMethod]
    public void TroopPrice_Tier3AtWar_StacksBothMultipliers()
    {
        // 100 * 1.30 * 1.5 = 195
        Assert.AreEqual(195, _sut.TroopPrice(100, 3, atWar: true));
    }

    [TestMethod]
    public void TroopPrice_FractionalResult_RoundsToNearestDenar()
    {
        // 33 * 1.15 = 37.95 -> 38
        Assert.AreEqual(38, _sut.TroopPrice(33, 2, atWar: false));
    }

    [TestMethod]
    public void TroopPrice_TierZero_NoPremiumDiscount()
    {
        // A corrupt tier below 1 must not turn the premium into a discount.
        Assert.AreEqual(100, _sut.TroopPrice(100, 0, atWar: false));
    }

    [TestMethod]
    public void TroopPrice_NegativeTier_NoPremiumDiscount()
    {
        Assert.AreEqual(100, _sut.TroopPrice(100, -3, atWar: false));
    }

    [TestMethod]
    public void TroopPrice_ZeroVanillaCost_ReturnsZero()
    {
        Assert.AreEqual(0, _sut.TroopPrice(0, 5, atWar: true));
    }

    [TestMethod]
    public void TroopPrice_NegativeVanillaCost_ReturnsZero()
    {
        Assert.AreEqual(0, _sut.TroopPrice(-50, 5, atWar: true));
    }

    // ---- PlannedHours ----

    [TestMethod]
    public void PlannedHours_TypicalDistance_ScalesByHoursPerDistance()
    {
        Assert.AreEqual(20f, _sut.PlannedHours(10f));
    }

    [TestMethod]
    public void PlannedHours_ShortDistance_ClampsToMinimumTwoHours()
    {
        // 0.5 * 2 = 1h, below the source module's 2h floor.
        Assert.AreEqual(2f, _sut.PlannedHours(0.5f));
    }

    [TestMethod]
    public void PlannedHours_ZeroDistance_ClampsToMinimumTwoHours()
    {
        Assert.AreEqual(2f, _sut.PlannedHours(0f));
    }

    [TestMethod]
    public void PlannedHours_NegativeDistance_ClampsToMinimumTwoHours()
    {
        Assert.AreEqual(2f, _sut.PlannedHours(-5f));
    }

    [TestMethod]
    public void PlannedHours_NaNDistance_ClampsToMinimumTwoHours()
    {
        Assert.AreEqual(2f, _sut.PlannedHours(float.NaN));
    }

    [TestMethod]
    public void PlannedHours_InfiniteDistance_ClampsToMinimumTwoHours()
    {
        // Infinity fails the finiteness gate and collapses to zero distance, then the floor
        // applies. A caravan can never be scheduled to travel forever.
        Assert.AreEqual(2f, _sut.PlannedHours(float.PositiveInfinity));
    }

    [TestMethod]
    public void PlannedHours_NaNHoursSetting_ClampsToMinimumTwoHours()
    {
        _settings.CaravanHoursPerDistance.Returns(float.NaN);

        Assert.AreEqual(2f, _sut.PlannedHours(10f));
    }
}
