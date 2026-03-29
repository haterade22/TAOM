using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BattleBalance;
using TAOM.Features.BattleBalance.Models;

namespace TAOM.Tests.Features.BattleBalance;

[TestClass]
public class TaomMilitaryPowerModelTests
{
    private BattleBalanceConfig.TroopPowerSection _defaultConfig;

    [TestInitialize]
    public void Setup()
    {
        _defaultConfig = new BattleBalanceConfig.TroopPowerSection();
    }

    [TestMethod]
    public void CalculateTierPower_Tier7_ReturnsConfiguredValue()
    {
        // Arrange
        float tier7Value = 2.91f;

        // Act
        float result = TaomMilitaryPowerModel.CalculateTierPower(7, false, tier7Value, 3.26f, 3.61f, 3.96f, _defaultConfig);

        // Assert
        Assert.AreEqual(tier7Value, result, 0.001f);
    }

    [TestMethod]
    public void CalculateTierPower_Tier8_ReturnsConfiguredValue()
    {
        // Arrange
        float tier8Value = 3.26f;

        // Act
        float result = TaomMilitaryPowerModel.CalculateTierPower(8, false, 2.91f, tier8Value, 3.61f, 3.96f, _defaultConfig);

        // Assert
        Assert.AreEqual(tier8Value, result, 0.001f);
    }

    [TestMethod]
    public void CalculateTierPower_Tier9_ReturnsConfiguredValue()
    {
        // Arrange
        float tier9Value = 3.61f;

        // Act
        float result = TaomMilitaryPowerModel.CalculateTierPower(9, false, 2.91f, 3.26f, tier9Value, 3.96f, _defaultConfig);

        // Assert
        Assert.AreEqual(tier9Value, result, 0.001f);
    }

    [TestMethod]
    public void CalculateTierPower_Tier10_ReturnsConfiguredValue()
    {
        // Arrange
        float tier10Value = 3.96f;

        // Act
        float result = TaomMilitaryPowerModel.CalculateTierPower(10, false, 2.91f, 3.26f, 3.61f, tier10Value, _defaultConfig);

        // Assert
        Assert.AreEqual(tier10Value, result, 0.001f);
    }

    [TestMethod]
    public void CalculateTierPower_Tier6_VanillaFormula_Returns2Point56()
    {
        // Arrange
        // Vanilla formula: (2 + tier) * (10 + tier) * 0.02 = (2+6)*(10+6)*0.02 = 8*16*0.02 = 2.56
        float expected = (2f + 6f) * (10f + 6f) * 0.02f;

        // Act
        float result = TaomMilitaryPowerModel.CalculateTierPower(6, false, 2.91f, 3.26f, 3.61f, 3.96f, _defaultConfig);

        // Assert
        Assert.AreEqual(expected, result, 0.001f);
    }

    [TestMethod]
    public void CalculateTierPower_Tier6_OverrideEnabled_ReturnsConfigValue()
    {
        // Arrange
        float configT6Value = 2.56f; // The default config value for T6

        // Act
        float result = TaomMilitaryPowerModel.CalculateTierPower(6, true, 2.91f, 3.26f, 3.61f, 3.96f, _defaultConfig);

        // Assert
        Assert.AreEqual(configT6Value, result, 0.001f);
    }

    [TestMethod]
    public void CalculateTierPower_Tier11_FallsBackToFormulaViaConfig()
    {
        // Arrange
        // Tier 11 is not in the switch, falls through to config.GetTierPower(11)
        // Default config formula: (2 + 11) * (10 + 11) * 0.02 = 13 * 21 * 0.02 = 5.46
        float expected = (2f + 11f) * (10f + 11f) * 0.02f;

        // Act
        float result = TaomMilitaryPowerModel.CalculateTierPower(11, false, 2.91f, 3.26f, 3.61f, 3.96f, _defaultConfig);

        // Assert
        Assert.AreEqual(expected, result, 0.001f);
    }

    [TestMethod]
    [DataRow(7, 2.91f)]
    [DataRow(8, 3.26f)]
    [DataRow(9, 3.61f)]
    [DataRow(10, 3.96f)]
    public void CalculateTierPower_HighTierDefaults_MatchExpectedValues(int tier, float expected)
    {
        // Act
        float result = TaomMilitaryPowerModel.CalculateTierPower(tier, false, 2.91f, 3.26f, 3.61f, 3.96f, _defaultConfig);

        // Assert
        Assert.AreEqual(expected, result, 0.001f);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    public void CalculateTierPower_LowTierOverrideDisabled_UsesVanillaFormula(int tier)
    {
        // Arrange
        float expected = (2f + tier) * (10f + tier) * 0.02f;

        // Act
        float result = TaomMilitaryPowerModel.CalculateTierPower(tier, false, 2.91f, 3.26f, 3.61f, 3.96f, _defaultConfig);

        // Assert
        Assert.AreEqual(expected, result, 0.001f);
    }

    [TestMethod]
    [DataRow(0, 0.40f)]
    [DataRow(1, 0.66f)]
    [DataRow(2, 0.96f)]
    [DataRow(3, 1.30f)]
    [DataRow(4, 1.68f)]
    [DataRow(5, 2.10f)]
    public void CalculateTierPower_LowTierOverrideEnabled_UsesConfigValue(int tier, float expected)
    {
        // Act
        float result = TaomMilitaryPowerModel.CalculateTierPower(tier, true, 2.91f, 3.26f, 3.61f, 3.96f, _defaultConfig);

        // Assert
        Assert.AreEqual(expected, result, 0.001f);
    }
}
