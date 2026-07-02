using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SettlementEconomy;

namespace TAOM.Tests.Features.SettlementEconomy;

[TestClass]
public class SettlementEconomyServiceTests
{
    private SettlementEconomyService _sut = null!;

    private static SettlementEconomyConfig VanillaConfig() => new SettlementEconomyConfig
    {
        TownGoldBase = 10000f,
        TownGoldPerProsperity = 12f,
        TownGoldRegenRate = 0.25f,
    };

    [TestInitialize]
    public void Setup() => _sut = new SettlementEconomyService();

    [TestMethod]
    public void ComputeTownGoldChange_VanillaConfig_MatchesVanillaFormula()
    {
        // DefaultSettlementEconomyModel.GetTownGoldChange (v1.4.6):
        // round(0.25 * (10000 + prosperity * 12 - gold)). P=640, G=0 -> 0.25 * 17680 = 4420.
        var result = _sut.ComputeTownGoldChange(640f, 0, VanillaConfig());

        Assert.AreEqual(4420, result);
    }

    [TestMethod]
    public void ComputeTownGoldChange_GoldAboveTarget_ReturnsNegativeChange()
    {
        // Mean-reversion above the equilibrium target must be preserved (no clamp):
        // 0.25 * (17680 - 30000) = -3080.
        var result = _sut.ComputeTownGoldChange(640f, 30000, VanillaConfig());

        Assert.AreEqual(-3080, result);
    }

    [TestMethod]
    public void ComputeTownGoldChange_GoldAtTarget_ReturnsZero()
    {
        var result = _sut.ComputeTownGoldChange(640f, 17680, VanillaConfig());

        Assert.AreEqual(0, result);
    }

    [TestMethod]
    public void ComputeTownGoldChange_ZeroProsperityZeroGold_ReturnsRateTimesBase()
    {
        // The structural recovery floor: even a fully prosperity-collapsed town regenerates
        // rate * base per day. Vanilla: 0.25 * 10000 = 2500.
        var result = _sut.ComputeTownGoldChange(0f, 0, VanillaConfig());

        Assert.AreEqual(2500, result);
    }

    [TestMethod]
    public void ComputeTownGoldChange_DefaultConfig_UsesShippedBuffedBase()
    {
        // Shipped defaults raise the base to 25000: collapsed town regen-at-zero 0.25 * 25000 = 6250.
        var result = _sut.ComputeTownGoldChange(0f, 0, new SettlementEconomyConfig());

        Assert.AreEqual(6250, result);
    }

    [TestMethod]
    public void ComputeTownGoldChange_RaisedBase_RaisesRegen()
    {
        var config = VanillaConfig();
        config.TownGoldBase = 25000f;

        // 0.25 * (25000 + 7680 - 0) = 8170.
        var result = _sut.ComputeTownGoldChange(640f, 0, config);

        Assert.AreEqual(8170, result);
    }

    [TestMethod]
    public void ComputeTownGoldChange_RaisedPerProsperity_ScalesWithProsperity()
    {
        var config = VanillaConfig();
        config.TownGoldPerProsperity = 24f;

        // 0.25 * (10000 + 640 * 24 - 0) = 0.25 * 25360 = 6340.
        var result = _sut.ComputeTownGoldChange(640f, 0, config);

        Assert.AreEqual(6340, result);
    }

    [TestMethod]
    public void ComputeTownGoldChange_RaisedRegenRate_AcceleratesRecovery()
    {
        var config = VanillaConfig();
        config.TownGoldRegenRate = 0.5f;

        // 0.5 * 17680 = 8840.
        var result = _sut.ComputeTownGoldChange(640f, 0, config);

        Assert.AreEqual(8840, result);
    }

    [TestMethod]
    public void ComputeTownGoldChange_NaNProsperity_TreatedAsZero()
    {
        var result = _sut.ComputeTownGoldChange(float.NaN, 0, VanillaConfig());

        Assert.AreEqual(2500, result);
    }

    [TestMethod]
    public void ComputeTownGoldChange_InfinityProsperity_TreatedAsZero()
    {
        var result = _sut.ComputeTownGoldChange(float.PositiveInfinity, 0, VanillaConfig());

        Assert.AreEqual(2500, result);
    }

    [TestMethod]
    public void ComputeTownGoldChange_NullConfig_FallsBackToVanillaConstants()
    {
        // Defensive parity with SettlementFoodService's null tolerance: a null config must never
        // NRE the daily tick — it computes with the hardcoded vanilla constants.
        var result = _sut.ComputeTownGoldChange(640f, 0, null);

        Assert.AreEqual(4420, result);
    }

    [TestMethod]
    public void ComputeTownGoldChange_MidpointValue_UsesBankersRoundingLikeVanilla()
    {
        // TaleWorlds MathF.Round is (int)Math.Round(f) — banker's rounding. Pin the parity:
        // 0.25 * 10002 = 2500.5 -> 2500 (rounds to even); 0.25 * 10006 = 2501.5 -> 2502.
        var configHalfDown = VanillaConfig();
        configHalfDown.TownGoldBase = 10002f;
        var configHalfUp = VanillaConfig();
        configHalfUp.TownGoldBase = 10006f;

        Assert.AreEqual(2500, _sut.ComputeTownGoldChange(0f, 0, configHalfDown));
        Assert.AreEqual(2502, _sut.ComputeTownGoldChange(0f, 0, configHalfUp));
    }

    [TestMethod]
    public void DefaultConfig_MatchesShippedDefaults()
    {
        // Pin the shipped defaults (#317): base buffed to 25000, slope and rate stay vanilla.
        var config = new SettlementEconomyConfig();

        Assert.AreEqual(25000f, config.TownGoldBase, 0.001f);
        Assert.AreEqual(12f, config.TownGoldPerProsperity, 0.001f);
        Assert.AreEqual(0.25f, config.TownGoldRegenRate, 0.001f);
    }
}
