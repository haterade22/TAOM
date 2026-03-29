using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BattleBalance.Models;

namespace TAOM.Tests.Features.BattleBalance;

[TestClass]
public class TaomCombatSimulationModelTests
{
    [TestMethod]
    public void CalculateBluntChance_PlayerBattle_ReturnsPlayerChance()
    {
        // Arrange
        float playerChance = 0.30f;
        float aiChance = 0.10f;

        // Act
        float result = TaomCombatSimulationModel.CalculateBluntChance(true, playerChance, aiChance);

        // Assert
        Assert.AreEqual(playerChance, result, 0.001f);
    }

    [TestMethod]
    public void CalculateBluntChance_AIBattle_ReturnsAIChance()
    {
        // Arrange
        float playerChance = 0.30f;
        float aiChance = 0.10f;

        // Act
        float result = TaomCombatSimulationModel.CalculateBluntChance(false, playerChance, aiChance);

        // Assert
        Assert.AreEqual(aiChance, result, 0.001f);
    }

    [TestMethod]
    public void CalculateBluntChance_PlayerChance_MatchesInput()
    {
        // Arrange
        float playerChance = 0.45f;

        // Act
        float result = TaomCombatSimulationModel.CalculateBluntChance(true, playerChance, 0.05f);

        // Assert
        Assert.AreEqual(playerChance, result, 0.001f);
    }

    [TestMethod]
    public void CalculateBluntChance_AIChance_MatchesInput()
    {
        // Arrange
        float aiChance = 0.15f;

        // Act
        float result = TaomCombatSimulationModel.CalculateBluntChance(false, 0.30f, aiChance);

        // Assert
        Assert.AreEqual(aiChance, result, 0.001f);
    }

    [TestMethod]
    [DataRow(true, 0.30f, 0.10f, 0.30f)]
    [DataRow(false, 0.30f, 0.10f, 0.10f)]
    [DataRow(true, 0.50f, 0.05f, 0.50f)]
    [DataRow(false, 0.50f, 0.05f, 0.05f)]
    public void CalculateBluntChance_VariousInputs_ReturnsCorrectChance(
        bool isPlayerBattle, float playerChance, float aiChance, float expected)
    {
        // Act
        float result = TaomCombatSimulationModel.CalculateBluntChance(isPlayerBattle, playerChance, aiChance);

        // Assert
        Assert.AreEqual(expected, result, 0.001f);
    }
}
