using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BattleBalance;
using TAOM.Features.BattleBalance.Models;

namespace TAOM.Tests.Features.BattleBalance;

[TestClass]
public class TaomPartyHealingModelTests
{
    [TestMethod]
    public void ApplyCulturalSurvivalBonus_ZeroBonus_ReturnsVanillaUnchanged()
    {
        // Arrange
        float vanillaSurvival = 0.75f;

        // Act
        float result = TaomPartyHealingModel.ApplyCulturalSurvivalBonus(vanillaSurvival, 0f);

        // Assert
        Assert.AreEqual(vanillaSurvival, result, 0.001f);
    }

    [TestMethod]
    public void ApplyCulturalSurvivalBonus_PositiveBonus_ReducesDeathChance()
    {
        // Arrange
        float vanillaSurvival = 0.75f; // deathChance = 0.25
        float bonus = 0.3f;            // newDeathChance = 0.25 * (1 - 0.3) = 0.25 * 0.7 = 0.175
        float expectedSurvival = 1f - 0.175f; // 0.825

        // Act
        float result = TaomPartyHealingModel.ApplyCulturalSurvivalBonus(vanillaSurvival, bonus);

        // Assert
        Assert.AreEqual(expectedSurvival, result, 0.001f);
    }

    [TestMethod]
    public void ApplyCulturalSurvivalBonus_NegativeBonus_IncreasesDeathChance()
    {
        // Arrange
        float vanillaSurvival = 0.75f; // deathChance = 0.25
        float bonus = -0.2f;           // newDeathChance = 0.25 * (1 - (-0.2)) = 0.25 * 1.2 = 0.30
        float expectedSurvival = 1f - 0.30f; // 0.70

        // Act
        float result = TaomPartyHealingModel.ApplyCulturalSurvivalBonus(vanillaSurvival, bonus);

        // Assert
        Assert.AreEqual(expectedSurvival, result, 0.001f);
    }

    [TestMethod]
    public void ApplyCulturalSurvivalBonus_LargePositiveBonus_ClampedToOne()
    {
        // Arrange
        float vanillaSurvival = 0.99f; // deathChance = 0.01
        float bonus = 0.999f;          // newDeathChance = 0.01 * 0.001 = 0.00001, survival ≈ 0.99999
        // Should clamp to 1.0f

        // Act
        float result = TaomPartyHealingModel.ApplyCulturalSurvivalBonus(vanillaSurvival, bonus);

        // Assert
        Assert.IsTrue(result <= 1.0f, "Survival chance must not exceed 1.0");
        Assert.IsTrue(result >= 0.99f, "Survival chance with large positive bonus must be very high");
    }

    [TestMethod]
    public void ApplyCulturalSurvivalBonus_LargeNegativeBonus_ClampedToZero()
    {
        // Arrange
        float vanillaSurvival = 0.10f; // deathChance = 0.90
        float bonus = -10f;            // newDeathChance = 0.90 * 11 = 9.9, survival = 1-9.9 = negative
        // Should clamp to 0.0f

        // Act
        float result = TaomPartyHealingModel.ApplyCulturalSurvivalBonus(vanillaSurvival, bonus);

        // Assert
        Assert.AreEqual(0f, result, 0.001f, "Survival chance must not go below 0.0");
    }

    [TestMethod]
    [DataRow(0.75f, 0.30f, 0.825f)]  // Gondor: +30% survival bonus
    [DataRow(0.75f, 0.20f, 0.80f)]   // Rohan: +20% survival bonus
    [DataRow(0.75f, 0.50f, 0.875f)]  // Lothlorien: +50% survival bonus
    [DataRow(0.75f, -0.20f, 0.70f)]  // Mordor: -20% survival penalty
    [DataRow(0.75f, -0.10f, 0.725f)] // Gundabad: -10% survival penalty
    public void ApplyCulturalSurvivalBonus_KnownValues_ReturnsExpected(
        float vanillaSurvival, float bonus, float expected)
    {
        // Act
        float result = TaomPartyHealingModel.ApplyCulturalSurvivalBonus(vanillaSurvival, bonus);

        // Assert
        Assert.AreEqual(expected, result, 0.001f);
    }
}

[TestClass]
public class BattleBalanceConfigCasualtyRatiosTests
{
    [TestMethod]
    public void GetCulturalSurvivalBonus_KnownCulture_ReturnsBonus()
    {
        // Arrange
        var config = new BattleBalanceConfig.CasualtyRatiosSection();

        // Act
        float result = config.GetCulturalSurvivalBonus("gondor");

        // Assert
        Assert.AreEqual(0.3f, result, 0.001f);
    }

    [TestMethod]
    public void GetCulturalSurvivalBonus_UnknownCulture_ReturnsZero()
    {
        // Arrange
        var config = new BattleBalanceConfig.CasualtyRatiosSection();

        // Act
        float result = config.GetCulturalSurvivalBonus("some_unknown_culture");

        // Assert
        Assert.AreEqual(0f, result, 0.001f);
    }

    [TestMethod]
    public void GetCulturalSurvivalBonus_NegativeCulture_ReturnsNegativeBonus()
    {
        // Arrange
        var config = new BattleBalanceConfig.CasualtyRatiosSection();

        // Act
        float result = config.GetCulturalSurvivalBonus("mordor");

        // Assert
        Assert.AreEqual(-0.2f, result, 0.001f);
    }

    [TestMethod]
    [DataRow("gondor", 0.3f)]
    [DataRow("vlandia", 0.2f)]
    [DataRow("lothlorien", 0.5f)]
    [DataRow("erebor", 0.3f)]
    [DataRow("rivendell", 0.4f)]
    [DataRow("mordor", -0.2f)]
    [DataRow("gundabad", -0.1f)]
    [DataRow("dolguldur", -0.1f)]
    public void GetCulturalSurvivalBonus_AllDefinedCultures_ReturnExpectedBonus(
        string cultureId, float expected)
    {
        // Arrange
        var config = new BattleBalanceConfig.CasualtyRatiosSection();

        // Act
        float result = config.GetCulturalSurvivalBonus(cultureId);

        // Assert
        Assert.AreEqual(expected, result, 0.001f);
    }
}

[TestClass]
public class BattleBalanceConfigTroopPowerTests
{
    [TestMethod]
    public void GetTierPower_T7_ReturnsConfiguredValue()
    {
        // Arrange
        var config = new BattleBalanceConfig.TroopPowerSection();

        // Act
        float result = config.GetTierPower(7);

        // Assert
        Assert.AreEqual(2.91f, result, 0.001f);
    }

    [TestMethod]
    public void GetTierPower_UnknownTier_FallsBackToFormula()
    {
        // Arrange
        var config = new BattleBalanceConfig.TroopPowerSection();
        // Formula: (2 + tier) * (10 + tier) * 0.02
        float expected = (2f + 15f) * (10f + 15f) * 0.02f; // tier 15

        // Act
        float result = config.GetTierPower(15);

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
    [DataRow(6, 2.56f)]
    [DataRow(7, 2.91f)]
    [DataRow(8, 3.26f)]
    [DataRow(9, 3.61f)]
    [DataRow(10, 3.96f)]
    public void GetTierPower_AllDefinedTiers_ReturnExpectedValues(int tier, float expected)
    {
        // Arrange
        var config = new BattleBalanceConfig.TroopPowerSection();

        // Act
        float result = config.GetTierPower(tier);

        // Assert
        Assert.AreEqual(expected, result, 0.001f);
    }
}
