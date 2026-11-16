using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BattleBalance;
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

    // Wiring only (#1283): the registration hands the model the config provider, and SimulateHit passes the striker's
    // power first, the struck troop's second, each read from its own party, and the configured exponent. The decision is
    // pinned in AutoResolveQualityMathTests and AutoResolveQualityApplyTests.
    [TestMethod]
    public void Wiring_SubModulePassesTheConfig_AndSimulateHitAppliesTheQualityFactor()
    {
        var subModule = TAOM.Tests.Infrastructure.RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);
        var model = TAOM.Tests.Infrastructure.RepoPaths.ReadSource(
            "Main/Features/BattleBalance/Models/TaomCombatSimulationModel.cs", stripComments: true);

        StringAssert.Contains(subModule,
            "new TaomCombatSimulationModel(battleBalanceSettings, IoC.Resolve<Features.Refuge.IRefugeDefenseService>(), battleBalanceConfig)");
        StringAssert.Contains(model,
            "AutoResolveQualityMath.Apply(ref result, TroopPower(strikerTroop, strikerParty), TroopPower(struckTroop, struckParty), PowerRatioExponent);");
        StringAssert.Contains(model,
            "PowerRatioExponent => _configProvider?.GetConfig()?.AutoResolve?.PowerRatioExponent ?? AutoResolveQualityMath.VanillaPowerRatioExponent;");
    }

    [DataTestMethod]
    [DataRow(true, "bluecraig", 0.1f)]
    [DataRow(false, "bluecraig", 0f)]
    [DataRow(true, "gondor", 0f)]
    [DataRow(true, null, 0f)]
    public void SiegeDefenceBonus_AppliesOnlyToASiegeOfAFortificationTheCultureOwns(bool isSiege, string ownerCulture, float expected)
    {
        var section = new BattleBalanceConfig.SiegeDefenceSection();

        Assert.AreEqual(expected, TaomCombatSimulationModel.SiegeDefenceBonus(isSiege, ownerCulture, section), 0.0001f);
    }

    [TestMethod]
    public void SiegeDefenceBonus_NoSection_IsZero()
    {
        Assert.AreEqual(0f, TaomCombatSimulationModel.SiegeDefenceBonus(true, "bluecraig", null), 0.0001f);
    }

    [TestMethod]
    public void Wiring_GetBattleAdvantage_AddsTheSiegeDefenceBonusToTheDefenders()
    {
        var model = TAOM.Tests.Infrastructure.RepoPaths.ReadSource(
            "Main/Features/BattleBalance/Models/TaomCombatSimulationModel.cs", stripComments: true);

        StringAssert.Contains(model, "base.GetBattleAdvantage(mapEvent, out defenderAdvantage, out attackerAdvantage);");
        StringAssert.Contains(model, "SiegeDefenceBonus(mapEvent.EventType == MapEvent.BattleTypes.Siege,");
        StringAssert.Contains(model, "mapEvent.MapEventSettlement?.MapFaction?.Culture?.StringId");
        StringAssert.Contains(model, "defenderAdvantage.AddFactor(bonus);");
    }
}
